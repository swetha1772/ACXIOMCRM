using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.Attributes
{
    public class NotInPastDateAttribute : ValidationAttribute
    {
        public NotInPastDateAttribute()
        {
            ErrorMessage = "Date cannot be in the past.";
        }

        public override bool IsValid(object? value)
        {
            if (value is null) return true;

            if (value is DateTime dt)
            {
                return dt.Date >= DateTime.Today;
            }

            return false;
        }
    }
}
