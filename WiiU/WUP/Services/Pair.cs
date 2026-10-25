namespace WiiU.WUP.Services
{
    public class Pair<T, K>(T key, K value)
    {
        public T Key { get; set; } = key;

        public K Value { get; set; } = value;
    }
}