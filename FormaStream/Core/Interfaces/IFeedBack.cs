using System.Threading.Tasks;

namespace FormaStream.Core.Interfaces;

public interface IFeedBack
{
    void SendToGoogleForm(string formUrl, string name, string email, string message);

    Task<string> SubmitFeedbackAsync(string type, string? feedbackText);
}