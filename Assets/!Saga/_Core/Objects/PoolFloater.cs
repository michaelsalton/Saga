using UnityEngine;
using Saga.Rendering;

namespace Saga
{
    /// <summary>
    /// Drifts an object around a rectangular patch of water and rides it on the swell, so a floaty
    /// (pool tube, lily pad, crate) reads as adrift rather than parked.
    ///
    /// The area is an axis-aligned XZ rectangle centred on wherever the object sits when the component
    /// enables, plus <see cref="areaOffset"/>. Heading and speed come from Perlin noise so the path never
    /// repeats; inside the edge margin the heading is steered back inward, and the position is clamped as
    /// a backstop, so the object can never leave the rectangle.
    ///
    /// Vertical motion is a REAL sample of the water surface, via <see cref="WaterSurface"/> — a C# mirror
    /// of the Gerstner displacement in WaterWaves.hlsl, sampled at the object's own XZ against the same
    /// _Time.y the shader sees. The object holds the draft it was authored with (its height above the
    /// water's rest plane, measured once on enable) and tracks the surface from there, so it sits ON the
    /// water at a fixed height however the swell moves under it.
    ///
    /// The old sine bob survives as <see cref="fallbackBob"/>, used only when there is no water grid to
    /// sample — keeping the component usable on a puddle with no Saga/Water surface under it.
    ///
    /// All of it runs on a STEPPED clock (<see cref="stepFps"/>), floored onto a coarse time grid exactly
    /// as SagaWindBend does to the grass — so the floaty ticks along with the grass instead of gliding past
    /// it at frame rate. Set it to 0 for smooth motion.
    /// </summary>
    [AddComponentMenu("Saga/Objects/Pool Floater")]
    public class PoolFloater : MonoBehaviour
    {
        [Header("Area")]
        [Tooltip("Size of the drift rectangle on X and Z, in metres. Centred on the object's position when enabled.")]
        [SerializeField] Vector2 areaSize = new Vector2(4f, 3f);

        [Tooltip("Shift the rectangle off the object's start position, in metres (X,Z). Use it when you drop the " +
                 "floaty at the pool's edge but want it to roam the whole pool.")]
        [SerializeField] Vector2 areaOffset = Vector2.zero;

        [Tooltip("Soft turn-back band inside each edge, in metres. The floaty starts curving away this far from " +
                 "the wall instead of bouncing off it.")]
        [Min(0f)]
        [SerializeField] float edgeMargin = 0.5f;

        [Tooltip("How hard the edge band steers, in degrees per second at full push.")]
        [Range(0f, 720f)]
        [SerializeField] float edgeTurnRate = 120f;

        [Header("Drift")]
        [Tooltip("Average travel speed in metres per second. A pool floaty is slow — 0.05 to 0.3.")]
        [Range(0f, 2f)]
        [SerializeField] float driftSpeed = 0.12f;

        [Tooltip("How much the speed wanders. 0 = dead constant, 1 = anything from a standstill to double speed.")]
        [Range(0f, 1f)]
        [SerializeField] float speedVariation = 0.6f;

        [Tooltip("How quickly the speed wanders, in changes per second. Low = long lazy surges.")]
        [Range(0.01f, 2f)]
        [SerializeField] float speedChangeRate = 0.15f;

        [Tooltip("Maximum wander of the heading, in degrees per second.")]
        [Range(0f, 180f)]
        [SerializeField] float turnRate = 25f;

        [Tooltip("How quickly the heading wanders, in changes per second. Low = long sweeping arcs.")]
        [Range(0.01f, 2f)]
        [SerializeField] float turnChangeRate = 0.12f;

        [Header("Water")]
        [Tooltip("The water surface to ride. Leave empty to find the WaterGrid in the scene when this enables. " +
                 "With no grid to sample, the object falls back to the sine bob below.")]
        [SerializeField] WaterGrid water;

        [Tooltip("Extra metres above the surface, on top of the draft the object was authored with. The draft " +
                 "itself is measured on enable, as the object's height above the water's REST plane — so it is " +
                 "not thrown off by whatever the swell happened to be doing on that frame. Leave at 0 and place " +
                 "the object at the height you want it to ride.")]
        [Range(-2f, 2f)]
        [SerializeField] float heightOffset = 0f;

        [Tooltip("Tilt the object onto the wave's own surface normal instead of the sine rock below. This is " +
                 "the honest version of the rock, and it costs nothing — the normal comes back from the same " +
                 "sample as the height.")]
        [SerializeField] bool alignToSurface = true;

        [Tooltip("Multiplies the angle away from vertical. Real wave normals are gentle: at the shipped " +
                 "M_Water amplitude the surface only tilts about 2 degrees at its steepest, which reads as " +
                 "less rock than the sines it replaces. 2 to 3 puts the motion back without inventing it.")]
        [Range(0f, 8f)]
        [SerializeField] float tiltExaggeration = 2.5f;

        [Header("Fallback Bob (no water grid)")]
        [Tooltip("Used ONLY when no water surface can be sampled. With a WaterGrid assigned or found, every " +
                 "value under this header is ignored and the real surface drives the height.")]
        [SerializeField] bool fallbackBob = true;

        [Tooltip("Peak rise and fall of the slow swell, in metres. Keep it small — at 27 px/metre, 0.05 is ~1.4 pixels.")]
        [Range(0f, 0.5f)]
        [SerializeField] float bobAmplitude = 0.05f;

        [Tooltip("Swells per second.")]
        [Range(0f, 4f)]
        [SerializeField] float bobFrequency = 0.4f;

        [Tooltip("Peak rise and fall of the faster ripple riding on top of the swell, in metres.")]
        [Range(0f, 0.25f)]
        [SerializeField] float rippleAmplitude = 0.015f;

        [Tooltip("Ripples per second.")]
        [Range(0f, 8f)]
        [SerializeField] float rippleFrequency = 1.7f;

        [Header("Tilt & Spin")]
        [Tooltip("Peak rock away from level, in degrees. Ignored while Align To Surface is on.")]
        [Range(0f, 30f)]
        [SerializeField] float rockAngle = 4f;

        [Tooltip("Rocks per second. Set it slightly off bobFrequency so the two drift in and out of step. " +
                 "Ignored while Align To Surface is on.")]
        [Range(0f, 4f)]
        [SerializeField] float rockFrequency = 0.55f;

        [Tooltip("Lazy turn about the floaty's up axis, in degrees per second.")]
        [Range(-90f, 90f)]
        [SerializeField] float spinSpeed = 6f;

        [Tooltip("How much the spin wanders. At 1 it occasionally reverses.")]
        [Range(0f, 1f)]
        [SerializeField] float spinVariation = 0.8f;

        [Header("Stepped Motion")]
        [Tooltip("Snap every bit of motion onto a coarse time grid, the way the grass wind does. 7 matches " +
                 "SAGA_WIND_FPS in GrassWind.hlsl, so the floaty ticks with the grass rather than gliding " +
                 "past it. 0 = smooth, frame-rate motion.\n\n" +
                 "This is stop-motion, not slow-motion: the object covers the same ground at the same average " +
                 "speed, it just arrives in visible increments. The height sample steps with it — at the " +
                 "shipped wave speed the surface only moves ~0.003 m across one step, well under a pixel, so " +
                 "the floaty does not visibly come off the water between ticks.")]
        [Range(0f, 30f)]
        [SerializeField] float stepFps = 7f;

        [Header("Sync")]
        [Tooltip("Give each instance its own noise stream and phase so a raft of floaties doesn't move in lockstep.")]
        [SerializeField] bool randomizeSeed = true;

        [Tooltip("Used when randomizeSeed is off — set it per instance to hand-separate a few floaties.")]
        [SerializeField] int seed = 0;

        [Header("Gizmo")]
        [SerializeField] bool drawArea = true;

        Vector3 startPos;
        Quaternion startRot;
        float heading;       // degrees, atan2(z, x)
        float yaw;           // accumulated spin, degrees
        float phase;         // seconds offset into the sines
        Vector2 noiseOrigin; // where this instance samples the Perlin field
        float draft;         // metres above the water's REST plane this object rides at
        float stepPhase01;   // where inside one step this instance's tick lands, 0..1
        float lastClock;     // stepped time of the last tick we actually moved on

        /// <summary>Centre of the drift rectangle, on XZ. Falls back to the live transform outside play mode so
        /// the gizmo tracks the object while you place it.</summary>
        Vector2 Center
        {
            get
            {
                Vector3 origin = Application.isPlaying ? startPos : transform.position;
                return new Vector2(origin.x + areaOffset.x, origin.z + areaOffset.y);
            }
        }

        void OnEnable()
        {
            // Rest pose is captured fresh each enable: the water level and the area both re-anchor to
            // wherever the object currently is, so re-enabling a drifted floaty doesn't teleport it back.
            startPos = transform.position;
            startRot = transform.rotation;

            if (water == null)
                water = FindFirstObjectByType<WaterGrid>();

            // Measured against the REST plane, not the live surface: capturing it off a displaced sample
            // would bake whatever the swell was doing on this one frame into the object's ride height
            // forever, so re-enabling on a crest would leave it permanently sitting high.
            draft = water != null ? startPos.y - WaterSurface.RestHeight(water, startPos) : 0f;

            var rng = new System.Random(randomizeSeed ? Random.Range(0, 100000) : seed);

            // Perlin is mirror-symmetric about the origin and lines up on the integer lattice, so park each
            // instance somewhere arbitrary in the field rather than sampling near (0,0).
            noiseOrigin = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
            phase = (float)rng.NextDouble() * 100f;
            heading = (float)rng.NextDouble() * 360f;
            yaw = 0f;

            stepPhase01 = (float)rng.NextDouble();
            lastClock = SteppedTime(WaterSurface.ShaderTime);
        }

        /// <summary>
        /// <paramref name="t"/> floored onto a stepFps grid, staggered per instance — the same trick
        /// SagaWindBend plays on _Time.y, which is what gives the grass its ticking motion.
        ///
        /// Unlike the grass, the per-instance offset is subtracted back out afterwards. Grass adds up to
        /// 1.5 s of phase and keeps it, which is harmless in a noise field but here would sample the water
        /// surface a second and a half out of date. Adding the offset before the floor and removing it after
        /// staggers WHEN each instance ticks without shifting WHAT time it thinks it is.
        /// </summary>
        float SteppedTime(float t)
        {
            if (stepFps <= 0f) return t;

            float step = 1f / stepFps;
            float off = stepPhase01 * step;
            return Mathf.Floor((t + off) / step) * step - off;
        }

        void Update()
        {
            // Between ticks the object holds completely still — that stillness IS the effect, so this early
            // return is the feature, not an optimisation. Everything downstream (drift, spin, the water
            // sample) rides this one clock, which is what keeps the stepped motion internally consistent.
            float clock = SteppedTime(WaterSurface.ShaderTime);
            if (stepFps > 0f && clock == lastClock) return;

            // A whole step's worth of travel, delivered at once. The average speed is identical to the
            // smooth path because the deltas are the real elapsed time, just batched.
            float dt = stepFps > 0f ? clock - lastClock : Time.deltaTime;
            lastClock = clock;

            float t = clock + phase;

            // Heading: wander freely, then bend away from any edge we're inside the margin of.
            heading += Noise(noiseOrigin.x, t * turnChangeRate) * turnRate * dt;

            Vector2 pos = new Vector2(transform.position.x, transform.position.z);
            Vector2 inward = EdgePush(pos);
            if (inward.sqrMagnitude > 0.000001f)
            {
                float target = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg;
                heading = Mathf.MoveTowardsAngle(heading, target, edgeTurnRate * Mathf.Clamp01(inward.magnitude) * dt);
            }

            float speed = Mathf.Max(0f, driftSpeed * (1f + Noise(noiseOrigin.x + 37f, t * speedChangeRate) * speedVariation));
            float rad = heading * Mathf.Deg2Rad;
            pos += new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (speed * dt);

            // Backstop — a speed spike or a shrunken area can outrun the soft steer.
            Vector2 c = Center;
            Vector2 half = areaSize * 0.5f;
            pos.x = Mathf.Clamp(pos.x, c.x - half.x, c.x + half.x);
            pos.y = Mathf.Clamp(pos.y, c.y - half.y, c.y + half.y);

            // Sample the real surface at the position we are about to move to, not the one we came from,
            // so height and XZ belong to the same instant. Sampled on `clock`, not `t`: t carries a
            // multi-second noise-stream offset that would read the wave field at the wrong moment
            // entirely, while clock is real time snapped to the tick grid.
            var sampleAt = new Vector3(pos.x, startPos.y, pos.y);
            float surfaceY = 0f;
            Vector3 surfaceNormal = Vector3.up;
            bool onWater = water != null &&
                           WaterSurface.TrySample(water, sampleAt, clock, out surfaceY, out surfaceNormal);

            float y;
            if (onWater)
            {
                y = surfaceY + draft + heightOffset;
            }
            else
            {
                float bob = fallbackBob
                    ? Mathf.Sin(t * bobFrequency * Mathf.PI * 2f) * bobAmplitude
                      + Mathf.Sin(t * rippleFrequency * Mathf.PI * 2f + 1.3f) * rippleAmplitude
                    : 0f;
                y = startPos.y + bob + heightOffset;
            }

            transform.position = new Vector3(pos.x, y, pos.y);

            // Rock/spin are applied in WORLD space (euler * startRot, not startRot * euler) so the tilt stays
            // about the water plane's axes however the model itself was authored.
            yaw += spinSpeed * (1f + Noise(noiseOrigin.y, t * 0.1f) * spinVariation) * dt;
            Quaternion spin = Quaternion.Euler(0f, yaw, 0f);

            if (onWater && alignToSurface)
            {
                // SlerpUnclamped, not Slerp: tiltExaggeration is meant to go above 1, and the clamped
                // overload would silently cap the whole control at "exactly the real normal".
                Quaternion toNormal = Quaternion.SlerpUnclamped(
                    Quaternion.identity, Quaternion.FromToRotation(Vector3.up, surfaceNormal), tiltExaggeration);

                transform.rotation = toNormal * spin * startRot;
            }
            else
            {
                float pitch = Mathf.Sin(t * rockFrequency * Mathf.PI * 2f) * rockAngle;
                float roll = Mathf.Sin(t * rockFrequency * Mathf.PI * 2f * 0.73f + 2.1f) * rockAngle;

                transform.rotation = Quaternion.Euler(pitch, yaw, roll) * startRot;
            }
        }

        /// <summary>Per-axis inward push, 0 at the margin's inner face and 1 at the wall.</summary>
        Vector2 EdgePush(Vector2 pos)
        {
            Vector2 c = Center;
            Vector2 half = areaSize * 0.5f;
            float m = Mathf.Max(edgeMargin, 0.0001f);
            Vector2 push = Vector2.zero;

            float dx = pos.x - c.x;
            float overX = Mathf.Abs(dx) - Mathf.Max(0f, half.x - m);
            if (overX > 0f) push.x = -Mathf.Sign(dx) * Mathf.Clamp01(overX / m);

            float dz = pos.y - c.y;
            float overZ = Mathf.Abs(dz) - Mathf.Max(0f, half.y - m);
            if (overZ > 0f) push.y = -Mathf.Sign(dz) * Mathf.Clamp01(overZ / m);

            return push;
        }

        /// <summary>Perlin remapped to -1..1. It rarely reaches the extremes, which is what makes it read as a
        /// gentle drift rather than Random.Range jitter.</summary>
        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;

        void OnDrawGizmosSelected()
        {
            if (!drawArea) return;

            Vector2 c = Center;
            float y = Application.isPlaying ? startPos.y : transform.position.y;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(c.x, y, c.y), new Vector3(areaSize.x, 0f, areaSize.y));

            if (edgeMargin > 0f)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                Gizmos.DrawWireCube(new Vector3(c.x, y, c.y),
                    new Vector3(Mathf.Max(0f, areaSize.x - edgeMargin * 2f), 0f,
                                Mathf.Max(0f, areaSize.y - edgeMargin * 2f)));
            }
        }
    }
}
