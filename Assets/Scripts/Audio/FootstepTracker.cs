// Turns ground distance travelled into footsteps until animation events take over
using UnityEngine;

namespace Hedgewitch.Audio
{
    public sealed class FootstepTracker
    {
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float travelled;

        public bool AnimationDriven { get; private set; }

        // True when a footstep should play this update
        public bool Step(Vector3 position, bool grounded, float strideLength)
        {
            if (!hasLastPosition)
            {
                lastPosition = position;
                hasLastPosition = true;
                return false;
            }

            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;

            float distance = delta.magnitude;
            strideLength = Mathf.Max(0.05f, strideLength);

            // Teleports and knockbacks are not walking
            if (AnimationDriven || !grounded || distance > strideLength * 3f)
            {
                return false;
            }

            travelled += distance;
            if (travelled < strideLength)
            {
                return false;
            }

            travelled %= strideLength;
            return true;
        }

        // Called once an animation event fires so the distance fallback stops doubling up
        public void MarkAnimationDriven()
        {
            AnimationDriven = true;
        }

        public void Reset(Vector3 position)
        {
            lastPosition = position;
            hasLastPosition = true;
            travelled = 0f;
        }
    }
}
