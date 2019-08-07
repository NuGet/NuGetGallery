namespace NuGetGallery.AccountDeleter
{
    public interface IUserEvaluatorFactory
    {
        IUserEvaluator GetEvaluatorForSource(string source);
    }
}
