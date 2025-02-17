﻿
using AutoMapper;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Marvin.Cache.Headers;

namespace CourseLibrary.API.Controllers;

[ApiController]
[HttpCacheExpiration (CacheLocation = CacheLocation.Public)]
[HttpCacheValidation (MustRevalidate = true)]
[Route("api/authors/{authorId}/courses")]
//[ResponseCache(CacheProfileName = "240SecondsCacheProfiles")]
public class CoursesController : ControllerBase
{
  private readonly ICourseLibraryRepository _courseLibraryRepository;
  private readonly IMapper _mapper;

  public CoursesController(ICourseLibraryRepository courseLibraryRepository,
      IMapper mapper)
  {
    _courseLibraryRepository = courseLibraryRepository ??
      throw new ArgumentNullException(nameof(courseLibraryRepository));
    _mapper = mapper ??
      throw new ArgumentNullException(nameof(mapper));
  }

  [HttpGet(Name="GetCoursesForAuthor")]
  public async Task<ActionResult<IEnumerable<CourseDto>>> GetCoursesForAuthor(Guid authorId)
  {
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }

    var coursesForAuthorFromRepo = await _courseLibraryRepository.GetCoursesAsync(authorId);
    return Ok(_mapper.Map<IEnumerable<CourseDto>>(coursesForAuthorFromRepo));
  }

  //[ResponseCache(Duration = 120)]
  [HttpGet("{courseId}", Name = "GetCourseForAuthor")]
  public async Task<ActionResult<CourseDto>> GetCourseForAuthor(Guid authorId, Guid courseId)
  {
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }

    var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

    if (courseForAuthorFromRepo == null)
    {
      return NotFound();
    }
    return Ok(_mapper.Map<CourseDto>(courseForAuthorFromRepo));
  }


  [HttpPost(Name = "CreateCourseForAuthor")]
  public async Task<ActionResult<CourseDto>> CreateCourseForAuthor(
      Guid authorId, CourseForCreationDto course)
  {
    /*
     * We don't care for authors (we don't use them after the check) 
     * No need to fetch the author and check for null
     */
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }

    var courseEntity = _mapper.Map<Entities.Course>(course);
    _courseLibraryRepository.AddCourse(authorId, courseEntity);
    await _courseLibraryRepository.SaveAsync();

    var courseToReturn = _mapper.Map<CourseDto>(courseEntity);
    return CreatedAtRoute("GetCourseForAuthor", new {authorId = authorId, courseId = courseToReturn.Id}, courseToReturn);
  }

  [HttpPut("{courseId}")]
  public async Task<IActionResult> UpdateCourseForAuthor(Guid authorId,
      Guid courseId,
      CourseForUpdateDto course)
  {
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }

    var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

    if (courseForAuthorFromRepo == null)
    {
      // Upserting if not present in database
      var courseToAdd = _mapper.Map<Entities.Course>(course);
      courseToAdd.Id = courseId;
      _courseLibraryRepository.AddCourse(authorId, courseToAdd);
      await _courseLibraryRepository.SaveAsync();

      var courseToReturn = _mapper.Map<CourseDto>(courseToAdd);
      return CreatedAtRoute("GetCourseForAuthor", new { authorId, courseId = courseId }, courseToReturn);
    }

    _mapper.Map(course, courseForAuthorFromRepo);

    _courseLibraryRepository.UpdateCourse(courseForAuthorFromRepo);

    await _courseLibraryRepository.SaveAsync();
    return NoContent();
  }

  [HttpPatch("{courseId}")]
  public async Task<IActionResult> PartiallyUpdateCourseForAuthor(Guid authorId, Guid courseId,
      JsonPatchDocument<CourseForUpdateDto> patchDocument)
  {
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }
    var courseEntity = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

    if (courseEntity == null)
    {
      var courseDto = new CourseForUpdateDto();
      patchDocument.ApplyTo(courseDto, ModelState);

      if (!TryValidateModel(courseDto))
      {
        return ValidationProblem(ModelState);
      }
      courseEntity = _mapper.Map<Entities.Course>(courseDto);
      courseEntity.Id = courseId;

      _courseLibraryRepository.AddCourse(authorId, courseEntity);
      await _courseLibraryRepository.SaveAsync();

      return CreatedAtRoute("GetCourseForAuthor", new { authorId , courseId },
          _mapper.Map<CourseDto>(courseEntity)); 
    }

    var courseToPatch= _mapper.Map<CourseForUpdateDto>(courseEntity);

    patchDocument.ApplyTo(courseToPatch, ModelState);

    if (!TryValidateModel(courseToPatch))
    {
      return ValidationProblem(ModelState);
    }

    _mapper.Map(courseToPatch, courseEntity);

    _courseLibraryRepository.UpdateCourse(courseEntity);
    await _courseLibraryRepository.SaveAsync();

    return NoContent();
  }

  [HttpDelete("{courseId}")]
  public async Task<ActionResult> DeleteCourseForAuthor(Guid authorId, Guid courseId)
  {
    if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
    {
      return NotFound();
    }

    var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

    if (courseForAuthorFromRepo == null)
    {
      return NotFound();
    }

    _courseLibraryRepository.DeleteCourse(courseForAuthorFromRepo);
    await _courseLibraryRepository.SaveAsync();

    return NoContent();
  }

  public override ActionResult ValidationProblem(
      [ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
  {
    var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>();

    return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext);
  }

}
