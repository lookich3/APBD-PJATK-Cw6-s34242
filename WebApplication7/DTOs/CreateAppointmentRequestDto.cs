using System.ComponentModel.DataAnnotations;

namespace WebApplication7.DTOs;

public class CreateAppointmentRequestDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int IdPatient { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int IdDoctor { get; set; }

    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;
}