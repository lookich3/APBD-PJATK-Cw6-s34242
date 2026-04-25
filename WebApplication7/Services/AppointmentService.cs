using WebApplication7.DTOs;
using WebApplication7.Exceptions;
using Microsoft.Data.SqlClient;

namespace WebApplication7.Services;

public class AppointmentService(IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        List<AppointmentListDto> appointments = [];

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = """
                              SELECT a.IdAppointment,
                                     a.AppointmentDate,
                                     a.Status,
                                     a.Reason,
                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                     p.Email
                              FROM Appointments a
                              JOIN Patients p ON p.IdPatient = a.IdPatient
                              WHERE (@status IS NULL OR a.Status = @status)
                                AND (@patientLastName IS NULL OR p.LastName = @patientLastName)
                              ORDER BY a.AppointmentDate
                              """;

        command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
        command.Parameters.AddWithValue("@patientLastName", string.IsNullOrWhiteSpace(patientLastName) ? DBNull.Value : patientLastName);

        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }

        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = """
                              SELECT a.IdAppointment,
                                     a.AppointmentDate,
                                     a.Status,
                                     a.Reason,
                                     a.InternalNotes,
                                     a.CreatedAt,
                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                     p.Email,
                                     p.PhoneNumber,
                                     d.FirstName + N' ' + d.LastName AS DoctorFullName,
                                     d.LicenseNumber,
                                     s.Name
                              FROM Appointments a
                              JOIN Patients p ON p.IdPatient = a.IdPatient
                              JOIN Doctors d ON d.IdDoctor = a.IdDoctor
                              JOIN Specializations s ON s.IdSpecialization = d.IdSpecialization
                              WHERE a.IdAppointment = @idAppointment
                              """;

        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new NotFoundException($"Nie znaleziono wizyty o id {idAppointment}");
        }

        return MapAppointmentDetails(reader);
    }

    public async Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate <= DateTime.UtcNow)
        {
            throw new BadRequestException("Data wizyty nie może być z przeszłości");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BadRequestException("Powód wizyty nie może być pusty");
        }

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        await connection.OpenAsync();

        command.Connection = connection;

        command.CommandText = """
                              SELECT 1
                              FROM Patients
                              WHERE IdPatient = @idPatient AND IsActive = 1
                              """;

        command.Parameters.AddWithValue("@idPatient", dto.IdPatient);

        var patientExists = await command.ExecuteScalarAsync();

        if (patientExists is null)
        {
            throw new BadRequestException($"Nie znaleziono aktywnego pacjenta o id {dto.IdPatient}");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              SELECT 1
                              FROM Doctors
                              WHERE IdDoctor = @idDoctor AND IsActive = 1
                              """;

        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);

        var doctorExists = await command.ExecuteScalarAsync();

        if (doctorExists is null)
        {
            throw new BadRequestException($"Nie znaleziono aktywnego lekarza o id {dto.IdDoctor}");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              SELECT 1
                              FROM Appointments
                              WHERE IdDoctor = @idDoctor
                                AND AppointmentDate = @appointmentDate
                                AND Status = N'Scheduled'
                              """;

        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@appointmentDate", dto.AppointmentDate);

        var appointmentConflict = await command.ExecuteScalarAsync();

        if (appointmentConflict is not null)
        {
            throw new ConflictException("Lekarz ma już zaplanowaną wizytę w tym terminie");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              INSERT INTO Appointments(IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                              OUTPUT INSERTED.IdAppointment
                              VALUES(@idPatient, @idDoctor, @appointmentDate, N'Scheduled', @reason)
                              """;

        command.Parameters.AddWithValue("@idPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@appointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@reason", dto.Reason.Trim());

        var newId = await command.ExecuteScalarAsync();

        return await GetAppointmentAsync(Convert.ToInt32(newId));
    }

    public async Task<AppointmentDetailsDto> UpdateAppointmentAsync(
        int idAppointment,
        UpdateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate <= DateTime.UtcNow)
        {
            throw new BadRequestException("Data wizyty nie może być z przeszłości");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BadRequestException("Powód wizyty nie może być pusty");
        }

        if (dto.Status is not ("Scheduled" or "Completed" or "Cancelled"))
        {
            throw new BadRequestException("Status musi mieć wartość Scheduled, Completed albo Cancelled");
        }

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        await connection.OpenAsync();

        command.Connection = connection;

        command.CommandText = """
                              SELECT AppointmentDate, Status
                              FROM Appointments
                              WHERE IdAppointment = @idAppointment
                              """;

        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        DateTime oldDate;
        string oldStatus;

        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new NotFoundException($"Nie znaleziono wizyty o id {idAppointment}");
            }

            oldDate = reader.GetDateTime(0);
            oldStatus = reader.GetString(1);
        }

        command.Parameters.Clear();

        if (oldStatus == "Completed" && oldDate != dto.AppointmentDate)
        {
            throw new ConflictException("Nie można zmienić daty zakończonej wizyty");
        }

        command.CommandText = """
                              SELECT 1
                              FROM Patients
                              WHERE IdPatient = @idPatient AND IsActive = 1
                              """;

        command.Parameters.AddWithValue("@idPatient", dto.IdPatient);

        var patientExists = await command.ExecuteScalarAsync();

        if (patientExists is null)
        {
            throw new NotFoundException($"Nie znaleziono aktywnego pacjenta o id {dto.IdPatient}");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              SELECT 1
                              FROM Doctors
                              WHERE IdDoctor = @idDoctor AND IsActive = 1
                              """;

        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);

        var doctorExists = await command.ExecuteScalarAsync();

        if (doctorExists is null)
        {
            throw new NotFoundException($"Nie znaleziono aktywnego lekarza o id {dto.IdDoctor}");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              SELECT 1
                              FROM Appointments
                              WHERE IdDoctor = @idDoctor
                                AND AppointmentDate = @appointmentDate
                                AND Status = N'Scheduled'
                                AND IdAppointment <> @idAppointment
                              """;

        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@appointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        var appointmentConflict = await command.ExecuteScalarAsync();

        if (appointmentConflict is not null)
        {
            throw new ConflictException("Lekarz ma już inną zaplanowaną wizytę w tym terminie");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              UPDATE Appointments
                              SET IdPatient = @idPatient,
                                  IdDoctor = @idDoctor,
                                  AppointmentDate = @appointmentDate,
                                  Status = @status,
                                  Reason = @reason,
                                  InternalNotes = @internalNotes
                              WHERE IdAppointment = @idAppointment
                              """;

        command.Parameters.AddWithValue("@idPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@idDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@appointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@status", dto.Status);
        command.Parameters.AddWithValue("@reason", dto.Reason.Trim());
        command.Parameters.AddWithValue("@internalNotes", dto.InternalNotes is null ? DBNull.Value : dto.InternalNotes.Trim());
        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        await command.ExecuteNonQueryAsync();

        return await GetAppointmentAsync(idAppointment);
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        await connection.OpenAsync();

        command.Connection = connection;

        command.CommandText = """
                              SELECT Status
                              FROM Appointments
                              WHERE IdAppointment = @idAppointment
                              """;

        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        var status = await command.ExecuteScalarAsync();

        if (status is null)
        {
            throw new NotFoundException($"Nie znaleziono wizyty o id {idAppointment}");
        }

        if (status.ToString() == "Completed")
        {
            throw new ConflictException("Nie można usunąć zakończonej wizyty");
        }

        command.Parameters.Clear();

        command.CommandText = """
                              DELETE FROM Appointments
                              WHERE IdAppointment = @idAppointment
                              """;

        command.Parameters.AddWithValue("@idAppointment", idAppointment);

        await command.ExecuteNonQueryAsync();
    }

    private static AppointmentDetailsDto MapAppointmentDetails(SqlDataReader reader)
    {
        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3),
            InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = reader.GetDateTime(5),
            PatientFullName = reader.GetString(6),
            PatientEmail = reader.GetString(7),
            PatientPhoneNumber = reader.GetString(8),
            DoctorFullName = reader.GetString(9),
            DoctorLicenseNumber = reader.GetString(10),
            Specialization = reader.GetString(11)
        };
    }
}