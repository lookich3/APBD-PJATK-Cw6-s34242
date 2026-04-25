using WebApplication7.DTOs;
using WebApplication7.Exceptions;
using WebApplication7.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication7.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        try
        {
            return Ok(await service.GetAppointmentsAsync(status, patientLastName));
        }
        catch (BadRequestException e)
        {
            return BadRequest(new ErrorResponseDto(e.Message));
        }
    }

    [HttpGet]
    [Route("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment([FromRoute] int idAppointment)
    {
        try
        {
            return Ok(await service.GetAppointmentAsync(idAppointment));
        }
        catch (NotFoundException e)
        {
            return NotFound(new ErrorResponseDto(e.Message));
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequestDto dto)
    {
        try
        {
            var appointment = await service.CreateAppointmentAsync(dto);

            return CreatedAtAction(
                nameof(GetAppointment),
                new { idAppointment = appointment.IdAppointment },
                appointment);
        }
        catch (BadRequestException e)
        {
            return BadRequest(new ErrorResponseDto(e.Message));
        }
        catch (NotFoundException e)
        {
            return NotFound(new ErrorResponseDto(e.Message));
        }
        catch (ConflictException e)
        {
            return Conflict(new ErrorResponseDto(e.Message));
        }
    }

    [HttpPut]
    [Route("{idAppointment:int}")]
    public async Task<IActionResult> UpdateAppointment(
        [FromRoute] int idAppointment,
        [FromBody] UpdateAppointmentRequestDto dto)
    {
        try
        {
            var appointment = await service.UpdateAppointmentAsync(idAppointment, dto);

            return Ok(appointment);
        }
        catch (BadRequestException e)
        {
            return BadRequest(new ErrorResponseDto(e.Message));
        }
        catch (NotFoundException e)
        {
            return NotFound(new ErrorResponseDto(e.Message));
        }
        catch (ConflictException e)
        {
            return Conflict(new ErrorResponseDto(e.Message));
        }
    }

    [HttpDelete]
    [Route("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment([FromRoute] int idAppointment)
    {
        try
        {
            await service.DeleteAppointmentAsync(idAppointment);

            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(new ErrorResponseDto(e.Message));
        }
        catch (ConflictException e)
        {
            return Conflict(new ErrorResponseDto(e.Message));
        }
    }
}