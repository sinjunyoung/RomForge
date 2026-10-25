namespace PBP.AT3.Services;

internal sealed class BitAllocHandler
{
    private long _targetBits;
    private float _minLambda;
    private float _maxLambda;
    private float _curLambda;
    private float _lastLambda;

    internal bool NeedRepeat;
    internal int CurEncPos;
    internal int RepeatEncPos;

    public void Start(long targetBits, float minLambda, float maxLambda)
    {
        _targetBits = targetBits;
        _minLambda = minLambda;
        _maxLambda = maxLambda;
        _lastLambda = maxLambda;
    }

    public float Continue()
    {
        if (_maxLambda <= _minLambda)
            return _lastLambda;

        _curLambda = (_maxLambda + _minLambda) / 2.0f;
        RepeatEncPos = CurEncPos;

        return _curLambda;
    }

    public bool Submit(long gotBits)
    {
        if (_maxLambda <= _minLambda)
            NeedRepeat = false;
        else
        {
            if (gotBits < _targetBits)
            {
                _lastLambda = _curLambda;
                _maxLambda = _curLambda - 0.01f;
                NeedRepeat = true;
            }
            else if (gotBits > _targetBits)
            {
                _minLambda = _curLambda + 0.01f;
                NeedRepeat = true;
            }
            else
                NeedRepeat = false;
        }

        return !NeedRepeat;
    }
}