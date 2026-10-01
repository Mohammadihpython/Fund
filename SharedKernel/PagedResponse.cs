namespace SharedKernel;


public class PagedResponse<T> : ResponseModel<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Uri? FirstPage { get; set; }
    public Uri? LastPage { get; set; }
    public int TotalPages { get; set; }
    public int? TotalRecords { get; set; }
    public int? FilteredCount { get; set; }
    public Uri? NextPage { get; set; }
    public Uri? PreviousPage { get; set; }
    
    public PagedResponse() { } 

    public PagedResponse(T data, int pageNumber, int pageSize)
    {
        this.Page = pageNumber;
        this.PageSize = pageSize;
        this.Data = data;
        this.Message = null;
        this.Errors = null;
    }
}