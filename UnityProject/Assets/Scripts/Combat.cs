using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum Faction { Defense, Invaders }
    public enum Role { Player, Marine, Runner, Spitter, Colossus, Command, Barracks, Bunker, Squad, Construction, Factory, Tank, EnemyTower }
    public enum Phase { Briefing, Assault, Intermission, Victory, Defeat }

    public sealed class Health : MonoBehaviour
    {
        public Faction Faction;
        public Role Role;
        public float Maximum, Current, AimHeight = 1f, Radius = .6f;
        public bool Alive = true;
        public bool IsSheltered;
        public float LastDamageTime = -100;
        public int Lane, Slot = -1;
        public ActorBrain Brain;
        public ArtRig Rig;
        public Vector3 WeakPoint;
        public float WeakRadius;
        public Vector3 Aim => transform.position + Vector3.up * AimHeight;
        public bool IsBuilding => Role == Role.Command || Role == Role.Barracks || Role == Role.Bunker || Role == Role.Construction || Role == Role.Factory || Role == Role.EnemyTower;

        public void Setup(Role role, Faction faction, float hp, float height, float radius)
        {
            Role = role; Faction = faction; Current = Maximum = hp; AimHeight = height; Radius = radius;
            GameWorld.I.Entities.Add(this);
        }

        public void Damage(float amount, Health source, bool critical = false)
        {
            if (!Alive || IsSheltered || GameWorld.I.Ended || (source != null && source.Faction == Faction)) return;
            var tech=GameWorld.I.Tech;
            if (Faction==Faction.Defense && (Role==Role.Player||Role==Role.Marine||Role==Role.Squad) &&
                tech!=null && tech.IsComplete(Research.InfantryArmor)) amount*=.85f;
            if (Role == Role.Colossus) amount *= tech!=null && tech.IsComplete(Research.ArmorPiercing) &&
                source!=null && source.Faction==Faction.Defense ? .90f : .72f;
            if (critical) amount *= 1.65f;
            float applied=Mathf.Min(Current,amount);
            GameWorld.I.Stats?.RecordDamage(this,applied);
            Current = Mathf.Max(0, Current - amount);
            LastDamageTime = GameWorld.I.SimTime;
            Rig?.Flash();
            if (Role == Role.Player) GameWorld.I.Player.Hurt(amount);
            if (source != null && source.Role == Role.Player)
                GameWorld.I.Player.HitConfirm(Current <= 0, critical,applied,Aim,GetInstanceID());
            if (Current > 0) return;
            Alive = false;
            GameWorld.I.OnKilled(this, source);
        }

        public void Heal(float amount) { if (Alive) Current = Mathf.Min(Maximum, Current + amount); }
        public bool WeakPointHit(Vector3 origin,Vector3 direction)
        {
            if(WeakRadius<=0)return false;
            Vector3 delta=transform.TransformPoint(WeakPoint)-origin;
            float along=Vector3.Dot(delta,direction);
            return along>0&&(delta-direction*along).sqrMagnitude<=WeakRadius*WeakRadius;
        }
    }

    public static class Combat
    {
        static readonly RaycastHit[] Hits = new RaycastHit[128];
        public const int WorldLayer = 8, UnitLayer = 9, PlayerLayer = 10;
        public const int WorldMask = 1 << WorldLayer;
        public const int ShotMask = (1 << WorldLayer) | (1 << UnitLayer) | (1 << PlayerLayer);

        public static bool Trace(Vector3 start, Vector3 direction, float distance, Health ignore, out RaycastHit hit,bool passFriendlyUnits=false)
        {
            hit = default;
            int count = Physics.RaycastNonAlloc(start, direction, Hits, distance, ShotMask, QueryTriggerInteraction.Collide);
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Health health = Hits[i].collider.GetComponentInParent<Health>();
                if ((ignore!=null&&health==ignore) || (health != null && (health.IsSheltered || !health.Alive&&!health.IsBuilding))) continue;
                if(passFriendlyUnits&&ignore!=null&&health!=null&&health.Faction==ignore.Faction&&!health.IsBuilding)continue;
                if (Hits[i].distance >= closest) continue;
                hit = Hits[i]; closest = hit.distance;
            }
            return closest < float.MaxValue;
        }

        public static bool Visible(Health from, Health target)
        {
            if(target==null||target.IsSheltered)return false;
            Vector3 delta = target.Aim - from.Aim;
            if (delta.sqrMagnitude < .01f) return true;
            if (!Trace(from.Aim, delta.normalized, delta.magnitude + .1f, from, out var hit,from.Role==Role.Squad||from.Role==Role.Player)) return true;
            return hit.collider.GetComponentInParent<Health>() == target;
        }

        public static void Rifle(Health from, Vector3 muzzle, Vector3 direction, float damage, bool player)
        {
            Vector3 end = muzzle + direction * 110;
            if (Trace(muzzle, direction, 110, from, out var hit,player||from.Role==Role.Squad))
            {
                end = hit.point;
                Health target = hit.collider.GetComponentInParent<Health>();
                bool critical = target != null && target.WeakPointHit(muzzle,direction);
                if(player&&target!=null)GameWorld.I.Squad.MarkTarget(target);
                if (target != null) target.Damage(damage, from, critical);
                GameWorld.I.Effects.Spark(end, target != null && target.Faction == Faction.Invaders ? Art.Pink : Art.Amber, .17f);
            }
            GameWorld.I.Effects.Tracer(muzzle, end, player ? Art.Cyan : Art.Amber, player ? .038f : .021f);
            if(player)GameWorld.I.Effects.Spark(muzzle,Art.Cyan,.12f);
            if (player) GameWorld.I.Sounds.Play(SoundId.Rifle, .45f);
            else GameWorld.I.Sounds.DistantShot(muzzle);
        }
    }

    public sealed class ActorBrain : MonoBehaviour
    {
        public Health Self;
        Health target;
        float think, attackCooldown, strafeSeed;
        int waypoint;
        Vector3 lastPosition;
        GuardNavigation guardNavigation;
        public Vector3 Station;
        public float Travelled;
        public AssaultGroup Group { get; private set; }
        public AssaultGroupState GroupState => Group==null ? AssaultGroupState.Engaged : Group.State;
        public Health Target => target;
        public static float MovementSpeed(Role role) => role==Role.Marine ? 3.7f : role==Role.Runner ? 4.5f : role==Role.Spitter ? 2.8f : 1.75f;

        public void AssignAssault(AssaultGroup group) { Group=group; }

        public void Setup(Health health, Vector3 station)
        {
            Self = health; Self.Brain = this; Station = station;
            think = UnityEngine.Random.Range(0, .3f);
            strafeSeed = UnityEngine.Random.Range(-1.5f, 1.5f);
            lastPosition = transform.position;
            if(Self.Role==Role.Marine)guardNavigation=new GuardNavigation();
        }

        public void Tick(float dt)
        {
            if (!Self.Alive) return;
            var world = GameWorld.I;
            if(Self.Role==Role.Marine)lastPosition=transform.position;
            if(Self.Role==Role.Marine&&world.Fort!=null&&world.Fort.TickGuard(this,dt))return;
            attackCooldown -= dt; think -= dt;
            bool friendly = Self.Faction == Faction.Defense;
            float range = Self.Role == Role.Bunker ? 28 : Self.Role == Role.Marine ? 25 : Self.Role == Role.Spitter ? 16 : Self.Role == Role.Colossus ? 3.4f : 1.85f;
            if (think <= 0 || target == null || !target.Alive || target.IsSheltered)
            {
                think = .22f + UnityEngine.Random.value * .16f;
                target = Self.Role==Role.Colossus ? FindSiegeTarget() : world.FindTarget(Self, friendly ? range + 3 : 15);
            }

            bool canAttack = target != null && target.Alive && !target.IsSheltered && Vector3.Distance(Self.Aim, target.Aim) < range + target.Radius;
            if (canAttack)
                canAttack = Combat.Visible(Self, target);
            if(!friendly&&Group!=null&&Group.State!=AssaultGroupState.Engaged)
            {
                if(canAttack)Group.Engage();
                else
                {
                    Move(Group.MemberGoal(Self),MovementSpeed(Self.Role),dt);
                    RecordMotion(dt);
                    return;
                }
            }
            Vector3 destination = transform.position;
            if (!canAttack)
            {
                if (friendly) destination = Station;
                else if(Self.Role==Role.Colossus&&target!=null&&target.IsBuilding&&Combat.Visible(Self,target))
                    destination=target.transform.position;
                else if (target != null && !target.IsBuilding && Vector3.Distance(transform.position, target.transform.position) < 12 && Combat.Visible(Self, target))
                    destination = target.transform.position;
                else
                {
                    // A formation may have advanced beyond the first route marker already.
                    while(waypoint<3&&transform.position.z<world.EnemyWaypoint(Self.Lane,waypoint,strafeSeed).z+1f)waypoint++;
                    Vector3 route = world.EnemyWaypoint(Self.Lane, waypoint, strafeSeed);
                    if (FlatDistance(transform.position, route) < 2.2f && waypoint < 3) waypoint++;
                    destination = world.EnemyWaypoint(Self.Lane, waypoint, strafeSeed);
                    if (waypoint >= 3 && world.Command.Alive) destination = world.Command.transform.position;
                }
            }
            if (Self.Role != Role.Bunker)
            {
                float speed = MovementSpeed(Self.Role);
                if (canAttack) speed = 0;
                if(Self.Role==Role.Marine)
                {
                    if(speed>0)NavigateTo(destination,dt);else RecordMotion(dt);
                }
                else Move(destination, speed, dt);
            }
            if (target != null && canAttack)
            {
                if(Self.Role != Role.Bunker)Face(target.transform.position, dt);
                Self.Rig?.AimAt(target.Aim);
                if (attackCooldown <= 0)
                {
                    switch (Self.Role)
                    {
                        case Role.Marine:
                            attackCooldown = .58f + UnityEngine.Random.Range(0, .12f);
                            Shoot(10 + 3 * world.DefenseLevel); break;
                        case Role.Bunker:
                            attackCooldown = .25f;
                            Shoot(world.BunkerDamage); break;
                        case Role.Spitter:
                            attackCooldown = 1.55f;
                            world.Effects.Bolt(Self.Aim + (target.Aim-Self.Aim).normalized * .9f, target.Aim, Self, 17, 19); break;
                        case Role.Runner:
                            attackCooldown = .82f;
                            target.Damage(13, Self); world.Effects.Spark(target.Aim, Art.Pink, .15f); break;
                        case Role.Colossus:
                            attackCooldown = 1.55f;
                            target.Damage(target.IsBuilding ? 78 : 42, Self);
                            world.Effects.Burst(target.transform.position + Vector3.up * .3f, Art.Amber, .65f); break;
                    }
                    Self.Rig?.Recoil();
                }
            }
            if(Self.Role!=Role.Marine)RecordMotion(dt);
        }

        Health FindSiegeTarget()
        {
            Health closest=null;float best=24;
            foreach(var h in GameWorld.I.Entities)
            {
                if(h==null||!h.Alive||!h.IsBuilding||h.Faction==Self.Faction||h.IsSheltered)continue;
                // Only the local entrance's defenses and the central command are siege objectives.
                if(h.Role!=Role.Command&&h.transform.position.x*(Self.Lane==0?-1:1)<-2)continue;
                float distance=Vector3.Distance(Self.Aim,h.Aim)-h.Radius;
                if(distance>=best||!Combat.Visible(Self,h))continue;
                best=distance;closest=h;
            }
            return closest!=null?closest:GameWorld.I.FindTarget(Self,12);
        }

        public void NavigateTo(Vector3 goal,float dt)
        {
            if(Self==null||!Self.Alive||Self.Role!=Role.Marine||Self.IsSheltered||dt<=0)return;
            if(guardNavigation==null)guardNavigation=new GuardNavigation();
            Vector3 before=transform.position;
            // Entering and leaving a bunker relocates an actor; only actual walking counts.
            lastPosition=before;
            transform.position=guardNavigation.Step(Self,goal,dt);
            Vector3 motion=transform.position-before;
            if(motion.sqrMagnitude>.000001f)Face(transform.position+motion,dt);
            RecordMotion(dt);
        }

        void RecordMotion(float dt)
        {
            float moved=Vector3.Distance(transform.position,lastPosition);Travelled+=moved;
            Self.Rig?.Animate(moved/Mathf.Max(dt,.0001f),dt);lastPosition=transform.position;
        }

        void Shoot(float damage)
        {
            Vector3 muzzle = Self.Rig != null && Self.Rig.Muzzle != null ? Self.Rig.Muzzle.position : Self.Aim;
            Combat.Rifle(Self, muzzle, (target.Aim - muzzle).normalized, damage, false);
        }

        void Move(Vector3 goal, float speed, float dt)
        {
            Vector3 desired = goal - transform.position; desired.y = 0;
            if (desired.magnitude < .6f || speed == 0) return;
            desired.Normalize();
            Vector3 separation = Vector3.zero;
            foreach (Health other in GameWorld.I.Entities)
            {
                if (other == null || other == Self || !other.Alive || other.IsSheltered || other.IsBuilding || other.Role == Role.Player) continue;
                Vector3 d = transform.position - other.transform.position; d.y = 0;
                float sq = d.sqrMagnitude, spacing = (Self.Radius + other.Radius) * .8f;
                if (sq > .005f && sq < spacing * spacing) separation += d.normalized * (1 - Mathf.Sqrt(sq) / spacing);
            }
            Vector3 direction = (desired + separation * 1.2f).normalized;
            Vector3 start = transform.position + Vector3.up * .8f;
            float check = speed * dt + Self.Radius * .8f;
            // Marines must clear walls with their full capsule, including when sliding around corners.
            float bodyRadius=Self.Role==Role.Marine?.42f:Self.Radius*.55f;
            if (Physics.SphereCast(start, bodyRadius, direction, out var hit, check, Combat.WorldMask, QueryTriggerInteraction.Ignore) && hit.normal.y<.6f)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(direction, hit.normal); tangent.y = 0;
                if (tangent.sqrMagnitude < .08f) tangent = Vector3.Cross(hit.normal, Vector3.up) * (Self.Lane == 0 ? 1 : -1);
                direction = tangent.normalized;
                if (Physics.SphereCast(start, Self.Role==Role.Marine?bodyRadius:Self.Radius*.5f, direction, out var tangentHit, check, Combat.WorldMask, QueryTriggerInteraction.Ignore) && tangentHit.normal.y<.6f) return;
            }
            Vector3 position = transform.position + direction * speed * dt;
            position.y = GameWorld.GroundHeight(position.x, position.z);
            transform.position = position;
            Face(position + direction, dt);
        }

        void Face(Vector3 point, float dt)
        {
            Vector3 delta = point - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Mathf.Min(1, dt * 9));
        }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    }

    [Serializable]
    public struct SpawnOrder
    {
        public Role Role; public int Lane;
        public SpawnOrder(Role role, int lane) { Role = role; Lane = lane; }
    }

    public static class WavePlan
    {
        public static readonly int[] Counts = { 40, 58, 92, 120, 150 };
        public static readonly float[] Intervals = { 1.2f, 1.35f, 1.2f, 1.2f, 1.15f };
        public static readonly string[] Titles = { "接触", "酸雨", "双线告急", "铁甲冲锋", "守住黎明" };
        public static int TotalCount { get { int total=0;foreach(int n in Counts)total+=n;return total; } }
        public static string Preview(int wave)
        {
            if(wave<1||wave>Counts.Length)return "五波攻势全部清除";
            int runners=0,spitters=0,armor=0;
            foreach(var order in Create(wave))
                if(order.Role==Role.Runner)runners++;else if(order.Role==Role.Spitter)spitters++;else armor++;
            string side=wave==1?"西侧入口":wave==2?"东侧入口":"东西双线";
            string units="迅行兽 "+runners;
            if(spitters>0)units+=" · 酸脊兽 "+spitters;
            if(armor>0)units+=" · 铁甲兽 "+armor;
            return side+" / 共 "+Counts[wave-1]+" 敌人 / "+units;
        }
        public static List<SpawnOrder> Create(int wave)
        {
            var orders = new List<SpawnOrder>();
            for (int i = 0; i < Counts[wave - 1]; i++)
            {
                Role role = Role.Runner;
                if (wave >= 2 && i % 4 == 2) role = Role.Spitter;
                if (wave == 3 && i % 10 == 8) role = Role.Colossus;
                // Alternate fourth-wave armor between the two three-unit lane blocks.
                if (wave == 4 && (i % 12 == 0 || i % 12 == 9)) role = Role.Colossus;
                if (wave == 5 && i % 5 == 0) role = Role.Colossus;
                if (wave == 5 && i % 5 == 3) role = Role.Spitter;
                int lane = wave == 1 ? 0 : wave == 2 ? 1 : (i / 3 + wave) % 2;
                orders.Add(new SpawnOrder(role, lane));
            }
            return orders;
        }
    }
}
