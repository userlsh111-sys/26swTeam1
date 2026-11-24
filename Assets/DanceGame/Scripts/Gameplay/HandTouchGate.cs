// One activation per entry. A new screen requires a fresh outside observation first.
public sealed class HandTouchGate
{
    private bool armed;
    public void Reset() { armed = false; }
    public bool Observe(bool tracked, bool inside)
    {
        if (!tracked) { Reset(); return false; }
        if (!inside) { armed = true; return false; }
        if (!armed) return false;
        armed = false;
        return true;
    }
}
