# Environment Metadata
$env:NUGET_GALLERY_ENV = "staging"
$env:NUGET_GALLERY_USE_EMULATOR = "false"
$env:NUGET_GALLERY_PROTECTED_DEPLOYMENT = "false" # True for production environments

# Database Connection:
# The Staging environment uses the Preview database server
#$env:NUGET_GALLERY_SQL_AZURE_SERVER = "tcp:cn9lv0qzry.database.windows.net"
#$env:NUGET_GALLERY_SQL_AZURE_DATABASE = "NuGetStaging"
#$env:NUGET_GALLERY_SQL_AZURE_USER = "nugetgallery-sa@cn9lv0qzry"
#$env:NUGET_GALLERY_SQL_AZURE_PASSWORD = "NuP@ckFTW"
#$env:NUGET_GALLERY_MAIN_CONNECTION_STRING = "Server=$env:NUGET_GALLERY_SQL_AZURE_SERVER;Database=$env:NUGET_GALLERY_SQL_AZURE_DATABASE;User ID=$env:NUGET_GALLERY_SQL_AZURE_USER;Password=$env:NUGET_GALLERY_SQL_AZURE_PASSWORD;Trusted_Connection=False;Encrypt=True;"

$env:NUGET_GALLERY_SQL_AZURE_SERVER = "tcp:amejn8fzzh.database.windows.net"
$env:NUGET_GALLERY_SQL_AZURE_DATABASE = "NuGetGallery"
$env:NUGET_GALLERY_SQL_AZURE_USER = "nugetgallery-sa@amejn8fzzh"
$env:NUGET_GALLERY_SQL_AZURE_PASSWORD = "NuP@ckFTW"
$env:NUGET_GALLERY_MAIN_CONNECTION_STRING = "Server=$env:NUGET_GALLERY_SQL_AZURE_SERVER;Database=$env:NUGET_GALLERY_SQL_AZURE_DATABASE;User ID=$env:NUGET_GALLERY_SQL_AZURE_USER;Password=$env:NUGET_GALLERY_SQL_AZURE_PASSWORD;Trusted_Connection=False;Encrypt=True;"

# Main Storage (The Staging environment uses the Preview blob storage account)
$env:NUGET_GALLERY_MAIN_STORAGE = "DefaultEndpointsProtocol=https;AccountName=nugetgallerypreview;AccountKey=1hLI1IaSbN1shLixvzZ0Z1xaFyuB01PNdxAMrOeYGv6drQQ5cF8qgBul7mgij7KOwKwgii9EA7+09nBSnRWm8Q=="

# Backup Source Storage
$env:NUGET_GALLERY_BACKUP_SOURCE_STORAGE = "DefaultEndpointsProtocol=https;AccountName=nugetgallery;AccountKey=RCQUnuXvp7KTX7FMqv6nU0sVC4TfZuu2Y7Ak/wojYie0f+pWH+LcZc+SA7I2iazbYiDepuGSqz1+nujf20i3mA=="

# Diagnostics Storage
$env:NUGET_GALLERY_DIAGNOSTICS_STORAGE = "DefaultEndpointsProtocol=https;AccountName=nugetgallerypreviewdiag;AccountKey=uXZ+6+G1TR6AFBtyTSwemyiE9xVg9yGKmI/VguzKFgPLXPz8Pr8OImyxqwnGMTUgTJLEOGNIXlfCg390WmmnJg=="

# Data Warehouse Connection
$env:NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING = "Server=tcp:cn9lv0qzry.database.windows.net;Database=NuGetWarehouseDev;User ID=nugetgallery-sa@cn9lv0qzry;Password=NuP@ckFTW;Trusted_Connection=False;Encrypt=True;"

# Backup Source Database
$env:NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING = "Server=tcp:amejn8fzzh.database.windows.net;Database=NuGetGallery;User ID=nugetgallery-sa@amejn8fzzh;Password=NuP@ckFTW;Trusted_Connection=False;Encrypt=True;"

# Azure Subscription Info (The Staging environment uses the Preview Subscription ID)
$env:NUGET_GALLERY_AZURE_SUBSCRIPTION_ID = "8282d469-4751-4e49-8ae6-86a405669429"
$env:NUGET_GALLERY_AZURE_SERVICE_NAME = "nugetgallery-staging"
$env:NUGET_GALLERY_AZURE_MANAGEMENT_CERTIFICATE_THUMBPRINT = "08E40392D848313AFBC3EC7D4A8CA462EBADCF8C"

# Cache Service
$env:NUGET_GALLERY_CACHE_SERVICE_ENDPOINT = "NuGetGalleryPrev.cache.windows.net"
$env:NUGET_GALLERY_CACHE_SERVICE_ACCESS_KEY = "YWNzOmh0dHBzOi8vbnVnZXRnYWxsZXJ5cHJldi1jYWNoZS5hY2Nlc3Njb250cm9sLndpbmRvd3MubmV0L1dSQVB2MC45LyZvd25lciZLV3FDQm54WmNFY3BpdnE3RnRjZzRBQTRxZENJS0ZwN0VyR2x1bnkraVVVPSZodHRwOi8vTnVHZXRHYWxsZXJ5UHJldi5jYWNoZS53aW5kb3dzLm5ldA=="

# Build Config (The Staging environment uses the Preview CDN host)
$env:NUGET_GALLERY_AZURE_VM_SIZE = "Small"
$env:NUGET_FACEBOOK_APP_ID = "235682883225736"
$env:NUGET_GALLERY_GOOGLE_ANALYTICS_PROPERTY_ID = "UA-30016961-2"
$env:NUGET_GALLERY_AZURE_CDN_HOST = "az320820.vo.msecnd.net"
$env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION = "2012-12-31T23:59:59.0000000-08:00"
$env:NUGET_GALLERY_REMOTE_DESKTOP_CERTIFICATE_THUMBPRINT = "4F9E542C81757DCA1A6BF42BCA568B0D91E10C79"
$env:NUGET_GALLERY_REMOTE_DESKTOP_ENCRYPTED_PASSWORD = "MIIBnQYJKoZIhvcNAQcDoIIBjjCCAYoCAQAxggFOMIIBSgIBADAyMB4xHDAaBgNVBAMME1dpbmRvd3MgQXp1cmUgVG9vbHMCEA/UypTMnTCkSBhhVK2OaS4wDQYJKoZIhvcNAQEBBQAEggEAMXT7E3e1w5zBd5Aj5wb67rp2O+ZWNPj3nHU93FzrAacIMwridbFHsJPvATMjmPF+JRybOo28tH2ZDTeb3bqNdVZ2HrHS8JBMGLnM+UYVkk9mMn3m1UOsP09HwCW1Pni+ukeVIu3ZIuoAEIrxRpXt1RaTPu6iZw2CN56bqVfEjymdeEXlg9+NYbtAiEbPQgHQDL+gtu7r3+UPWVO6p7bTtqFTkS8ARlYrPdnX7imUOSlwzWNANNofQDncjeG1ZyQMy6uJ0q91J25DIb9SelfoB9+CDOR5/UDdOfxYj5fuAjF0DEebD8CdUwwtWbeonHTunMYHBPz9xSpvn7w9gCEz5TAzBgkqhkiG9w0BBwEwFAYIKoZIhvcNAwcECM78axgBffICgBBgRA9DshH6o+RN+xPWTjI9"
$env:NUGET_GALLERY_REMOTE_DESKTOP_USERNAME = "nugetgallery-sa"
$env:NUGET_GALLERY_SSL_CERTIFICATE_THUMBPRINT = "290E396FD6252F99D09ADD40DC3A58CDC03B6DD0"
$env:NUGET_GALLERY_VALIDATION_KEY = "1bb4e3d402f6a6cfd90150f9a91b0db36fa853a95eb4f4febea89951d57bfae8"
$env:NUGET_GALLERY_DECRYPTION_KEY = "e301991cf175a3e7350e93364858a68d87b71e6e4f98463825d0e9425bda8a5b"

# Old Stuff
# TODO: Update the Gallery so that these parameters aren't needed (use connection strings and settings above instead)
$env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING = "$env:NUGET_GALLERY_MAIN_CONNECTION_STRING"
$env:NUGET_GALLERY_AZURE_STORAGE_BLOB_URL = "https://nugetgallerypreview.blob.core.windows.net"
$env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME = "nugetgallerypreview"
$env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY = "1hLI1IaSbN1shLixvzZ0Z1xaFyuB01PNdxAMrOeYGv6drQQ5cF8qgBul7mgij7KOwKwgii9EA7+09nBSnRWm8Q=="
$env:NUGET_GALLERY_AZURE_DIAG_STORAGE_ACCOUNT_NAME = "nugetgallerypreviewdiag"
$env:NUGET_GALLERY_AZURE_DIAG_STORAGE_ACCESS_KEY = "uXZ+6+G1TR6AFBtyTSwemyiE9xVg9yGKmI/VguzKFgPLXPz8Pr8OImyxqwnGMTUgTJLEOGNIXlfCg390WmmnJg=="

# Staging shares most resources with preview. Specifically:
# * SQL Azure Server (though it uses a different database)
# * Azure Storage
# * CDN Host
# * Cache Service
# * Data Warehouse
# * Backup Source
# * Subscription ID