namespace NuGetGallery
{
    public class SignInOrRegisterRequest
    {
        public SignInOrRegisterRequest(SignInRequest request)
        {
            SignInRequest = request;
        }

        public SignInOrRegisterRequest(RegisterRequest request)
        {
            RegisterRequest = request;
        }

        public SignInRequest SignInRequest { get; set; }
        public RegisterRequest RegisterRequest { get; set; }
    }
}