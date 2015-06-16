using System.Collections.Generic;

namespace Org.LoadRunner.Core.Models
{
    internal class ItemResult : BaseResult
    {
        public IList<RequestResult> Requests { get; set; }
    }
}
