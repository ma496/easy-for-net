namespace EasyForNet;

public static class IncrementalId
{
    private static readonly object LockObj = new object();
    private static int _id;

    public static int Id
    {
        get
        {
            lock (LockObj)
            {
                _id++;
                return _id;
            }
        }
    }
}