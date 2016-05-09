namespace Hjemat
{
    public enum CommandID
    {
        Error = 0,
        Ping = 1,
        Pingback = 2,
        Get = 3,
        Set = 4,
        Return = 5,
    }
    public enum CommandIDPair
    {
        Error = 0,
        Allow = 1,
        Ask = 2,
        Return = 3,
        Stop = 4
    }
}