namespace STS2WorkshopUploader.Workshop;

internal sealed class WorkshopLegalAgreementException : InvalidOperationException
{
    public WorkshopLegalAgreementException(string message) : base(message)
    {
    }
}
