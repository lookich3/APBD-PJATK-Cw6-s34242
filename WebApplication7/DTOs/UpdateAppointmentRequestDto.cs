using System.ComponentModel.DataAnnotations;

namespace WebApplication7.DTOs;

public class UpdateAppointmentRequestDto
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
    [MaxLength(30)]
    public string Status { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? InternalNotes { get; set; }
}