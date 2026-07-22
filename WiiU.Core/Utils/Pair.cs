namespace NUSPacker.Utils
{
    public class Pair<T, K>
    {
        public Pair(T key, K value)
        {
            Key = key;
            Value = value;
        }

        public T Key { get; set; }

        public K Value { get; set; }
    }
}
