namespace Tools
{
    public sealed class Countdown
    {
        private readonly int _maxCount;
        private readonly bool _autoReset;

        private int _currentCount;

        public Countdown(int maxCount, bool autoReset)
        {
            _maxCount = maxCount;
            _autoReset = autoReset;

            _currentCount = _maxCount;
        }
        
        public Countdown(int maxCount, int initialCount, bool autoReset)
        {
            _maxCount = maxCount;
            _autoReset = autoReset;

            _currentCount = initialCount;
        }

        public bool TickAndCheck()
        {
            if (--_currentCount > 0)
            {
                return false;
            }

            if (_autoReset)
            {
                _currentCount = _maxCount;
            }

            return true;
        }

        public void Reset()
        {
            _currentCount = _maxCount;
        }
    }
}