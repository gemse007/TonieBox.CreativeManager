# Latest Changes
surf to /settings page to manage
- TonieCloud Credentials
- Users (Add / Remove)
- Userdata (Name, Picture, Credits, Media -> Not Allowed / Allowed / Bought, ShowText (if the child can read))
- Vouchers

To Start you don't have to use docker anymore. You can just install .net core 6.0 Hosting Bundle from https://dotnet.microsoft.com/en-us/download/dotnet/6.0 and Start the TonieCreativeManager.UI2.exe
The only settings outside the /settings page are in the appsettings.json the Entries Settings/LibraryRoot which should show to the RootFolder and RepositoryDataFile which holds the data. 
If you want to run it as a service you can use powershell in Windows and use following Command (executed in Adminstration Mode)
New-Service -Name TonieBox -BinaryPathName pathto\TonieCreativeManager.UI2.exe

If you have a new User (e.g. your child) you should go to the Media part of the Settings and Hit Allow All, so that your child can surf through all the Media. "Not allowed" is if you have media, which your child sould not consume (e.g. he/her is to young).
If you use credits then you can give your child some credits to start with and tell the system how much to get for buying and uploading. You can easily amend the credits in the settings or else create vouchers (e.g. if you child did something good, for special reasons, ...)

# TonieBox Creative Manager

- C# Api to access tonie cloud
- Web frontend to manage content of your creative tonies

![alt](assets/overview.jpg)

## Setup (depreciated)

Create `.env` file

```
MYTONIE_LOGIN=
MYTONIE_PASSWORD=
MEDIA_LIBRARY=
HTTPS_PROXY=
```
- Use your toniecolud (https://meine.tonies.de) login in `MYTONIE_LOGIN` and `MYTONIE_PASSWORD`
- Set path in `MEDIA_LIBRARY` to your local media content
- `HTTPS_PROXY` is optional

## Run (depreciated)
```
docker-compose up
```
Open http://localhost:5995
