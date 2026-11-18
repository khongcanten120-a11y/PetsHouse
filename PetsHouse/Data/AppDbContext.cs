using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Models;

namespace PetsHouse.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public Microsoft.EntityFrameworkCore.DbSet<global::PetsHouse.Models.DoctorShiftRequest> DoctorShiftRequests => Set<global::PetsHouse.Models.DoctorShiftRequest>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
    public DbSet<ClinicService> ClinicServices => Set<ClinicService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Vaccination> Vaccinations => Set<Vaccination>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Customer>().HasIndex(x => x.UserId).IsUnique();
        model.Entity<Veterinarian>().HasIndex(x => x.UserId).IsUnique();
        model.Entity<MedicalRecord>().HasIndex(x => x.AppointmentId).IsUnique();
        model.Entity<Invoice>().HasIndex(x => x.AppointmentId).IsUnique();
        model.Entity<Appointment>().HasIndex(x => new { x.VeterinarianId, x.StartsAt });
        model.Entity<PrescriptionItem>().HasIndex(x => new { x.MedicalRecordId, x.MedicineId }).IsUnique();

        model.Entity<Customer>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Veterinarian>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Pet>().HasOne(x => x.Customer).WithMany(x => x.Pets).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne(x => x.Customer).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne(x => x.Pet).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne(x => x.ClinicService).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne(x => x.Veterinarian).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<MedicalRecord>().HasOne(x => x.Appointment).WithOne(x => x.MedicalRecord).HasForeignKey<MedicalRecord>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<PrescriptionItem>().HasOne(x => x.MedicalRecord).WithMany(x => x.Prescriptions).OnDelete(DeleteBehavior.Restrict);
        model.Entity<PrescriptionItem>().HasOne(x => x.Medicine).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockMovement>().HasOne(x => x.Medicine).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<Invoice>().HasOne(x => x.Appointment).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<InvoiceItem>().HasOne(x => x.Invoice).WithMany(x => x.Items).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WorkShift>().HasOne(x => x.Veterinarian).WithMany(x => x.WorkShifts).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Vaccination>().HasOne(x => x.Pet).WithMany().OnDelete(DeleteBehavior.Restrict);
        model.Entity<Vaccination>().HasOne(x => x.Veterinarian).WithMany().OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                property.SetPrecision(18);
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                property.SetScale(2);
    }
}
