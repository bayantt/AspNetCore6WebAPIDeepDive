namespace CourseLibrary.API.ResourceParameters;

public class AuthorsResourceParameters
{
  const int maxPageSize = 20;
  public string? MainCategory { get; set; }
  public string? SearchQuery { get; set; }
  public string? OrderBy { get; set; } = "name";
  public string? fields { get; set; }

  private int _pageSize = 10;
  public int PageSize
  {
    get => _pageSize;
    set => _pageSize = value > maxPageSize ? maxPageSize : value;
  }

  public int PageNumber { get; set; } = 1;
}
