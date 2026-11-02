namespace Shared.RabbitMq.Notifications.Templates
{
    public static class EmailTemplates
    {
        /// <summary>
        /// This template requires the following parameter:
        /// <para>
        /// Token
        /// </para>
        /// </summary>
        public const string EmailVerification = "email-verification";

        /// <summary>
        /// This template requires the following parameter:
        /// <para>
        /// Token
        /// </para>
        /// </summary>
        public const string ForgotPassword = "forgot-password";
    }
}
