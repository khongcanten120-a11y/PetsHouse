namespace PetsHouse.Models;

public class MonthlyRevenueVm
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Total { get; set; }
    public int InvoiceCount { get; set; }
}

public class ReportsVm
{
    public int Year { get; set; }
    public int Appointments { get; set; }
    public int Completed { get; set; }
    public int Canceled { get; set; }
    public decimal Revenue { get; set; }
    public List<MonthlyRevenueVm> Months { get; set; } = new();
}
