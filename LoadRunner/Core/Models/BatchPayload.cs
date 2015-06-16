namespace Org.LoadRunner.Core.Models
{
    public class BatchPayload : Payload
    {
        public string Name { get; set; }
        public int[] BatchSizes { get; set; }
    }
}
