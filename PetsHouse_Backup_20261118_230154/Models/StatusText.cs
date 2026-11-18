namespace PetsHouse.Models;

public static class StatusText
{
    public static string ToVietnamese(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Chờ xác nhận",
        AppointmentStatus.Confirmed => "Đã xác nhận",
        AppointmentStatus.Waiting => "Chờ khám",
        AppointmentStatus.InProgress => "Đang khám",
        AppointmentStatus.AwaitingPayment => "Chờ thanh toán",
        AppointmentStatus.Completed => "Hoàn thành",
        AppointmentStatus.Canceled => "Đã hủy",
        _ => "Không xác định"
    };

    public static string ToVietnamese(this PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Chưa thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        _ => "Không xác định"
    };
}
