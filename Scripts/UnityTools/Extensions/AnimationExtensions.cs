using UnityEngine;

namespace UnityTools
{
    public static class AnimationExtensions
    {
        public static void ActuallyRewindAnimation(this Animation animationToRewind)
        {
            animationToRewind.Play();
            animationToRewind.Rewind();
            animationToRewind.Sample();
            animationToRewind.Stop();
        }
    }
}