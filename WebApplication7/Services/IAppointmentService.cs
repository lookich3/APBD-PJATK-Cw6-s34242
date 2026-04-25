using WebApplication7.DTOs;

namespace WebApplication7.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);

    Task<AppointmentDetailsDto> GetAppointmentAsync(int idAppointment);

    Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto dto);

    Task<AppointmentDetailsDto> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto);

    Task DeleteAppointmentAsync(int idAppointment);
}