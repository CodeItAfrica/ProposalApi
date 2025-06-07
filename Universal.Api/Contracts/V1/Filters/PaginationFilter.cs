using System;
using System.ComponentModel.DataAnnotations;

namespace Gibs.OpenApi.Contracts.V1
{
    public class PaginationFilter
    {
        [Range(1, 1000)]
        public int PageNumber { get; set; } = 1;

        [Range(5, 500)]
        public int PageSize { get; set; } = 20;

    }

}
