# Emails

By default, the Gallery saves e-mail messages to the file system under `src\NuGetGallery\App_Data`.

You can use an SMTP server instead by editing `src\NuGetGallery\Web.config` and adding a `Gallery.SmtpUri`
setting. Its value should be an SMTP connection string, such as `smtp://user:password@smtpservername:25`.

You can require new accounts confirm their e-mail adddress by changing the value of `Gallery.ConfirmEmailAddresses`
to `true` in the `src\NuGetGallery\Web.config` file.
