using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class GetAllModel
    {
        public GetAllModel()
        {
            Filter = string.Empty;
            RowsPerPage = 10;
            PageNumber = 1;
        }

        public string Filter { get; set; }

        [Range(5, 100)]
        public int RowsPerPage { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; }
    }
}
