using System.Data;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Services;

public class BookingService(AppDbContext db)
{
    public async Task<(bool Ok,string Error)> BookAsync(int customerId, AppointmentVm vm)
    {
        if (!await db.Pets.AnyAsync(x => x.Id == vm.PetId && x.CustomerId == customerId))
            return (false, "Thú cưng không thuộc tài khoản.");
        var service = await db.ClinicServices.FirstOrDefaultAsync(x => x.Id == vm.ServiceId && x.IsActive);
        if (service is null) return (false,"Dịch vụ không hợp lệ.");
        if (vm.StartsAt <= DateTime.Now.AddMinutes(5)) return (false,"Chỉ được đặt giờ trong tương lai.");
        var finish = vm.StartsAt.AddMinutes(service.DurationMinutes);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (vm.VeterinarianId is int id)
        {
            var doctor = await db.Veterinarians.FirstOrDefaultAsync(x=> x.Id == id && x.IsActive);
            if (doctor is null) return (false,"Bác sĩ không hoạt động.");
            if (!await db.WorkShifts.AnyAsync(x=>x.VeterinarianId == id && x.StartAt<=vm.StartsAt && x.EndAt>=finish))
                return (false,"Bác sĩ không có ca làm việc trong thời gian này.");
            if (await db.Appointments.AnyAsync(x=>x.VeterinarianId==id && x.Status!=AppointmentStatus.Canceled && x.Status!=AppointmentStatus.Completed && x.StartsAt<finish && x.EndsAt>vm.StartsAt))
                return (false,"Bác sĩ đã có lịch khám trong thời gian này.");
        }
        db.Appointments.Add(new Appointment{CustomerId=customerId,PetId=vm.PetId,ClinicServiceId=vm.ServiceId,VeterinarianId=vm.VeterinarianId,StartsAt=vm.StartsAt,EndsAt=finish,Notes=vm.Notes});
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true,"");
    }
}
