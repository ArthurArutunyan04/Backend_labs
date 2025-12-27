namespace Consumer.Base
{
    public class Message<T>
    {
        public string Key { get; set; }

        public T Body { get; set; }
    }
}
