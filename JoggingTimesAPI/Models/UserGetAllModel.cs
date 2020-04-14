using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Models
{
    public class UserGetAllModel
    {
        public string Filter { get; set; }
        public string OrderByField { get; set; }
        public int RowsPerPage { get; set; }
        public int PageNumber { get; set; }
    }
}
