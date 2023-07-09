# UvA.TeamsLTI
LTI 1.3 app to create Teams for courses in an LMS. Brightspace and Canvas are currently supported.

Overview:
- The app is launched in the LMS via LTI 1.3.
- The backend handles the LTI launch and redirects to the frontend with a JWT (using the [LTI-consumer](https://github.com/UvA/LTI-consumer) .NET library).
- The Vue frontend allows the user to choose settings for team creation.
- The backend then uses the LMS API to retrieve the necessary data and the Graph API to create the team and sync the users to Teams.
- Created teams and synced users are stored in a MongoDB database.
- A nighty sync process updates the users in the team.

## Configuration
The tool can be configured to support one or more LMS instances using the `Environments` section in the app config and one or more Teams tenants using the `Teams` section, see the below example. 
Teams are generated with a mailnickname that contains the course ID and an optional `NicknamePrefix` specified in the config.
```json
{
  "Environments": {
    "Canvas": {
      "Authority": "https://canvas.test.instructure.com",
      "ClientId": "client id",
      "Endpoint": "https://canvas.test.instructure.com/api/lti/authorize_redirect",
      "JwksUrl": "https://canvas.test.instructure.com/api/lti/security/jwks",

      "OwnerId": "owner user ID from Azure AD",
      "Teams": "PRD",
      "NicknamePrefix": "canvas-dev",

      "Host": "https://uvadlo-dev.test.instructure.com",
      "Token": "canvas api token"
    },
    "Brightspace": {
      "Authority": "https://testhva.brightspace.com",
      "ClientId": "client id",
      "Endpoint": "https://testhva.brightspace.com/d2l/lti/authenticate",
      "JwksUrl": "https://testhva.brightspace.com/d2l/.well-known/jwks",

      "OwnerId": "owner user ID from Azure AD",
      "Teams": "PRD"
      "NicknamePrefix": "bsp-tst",

      "Host": "https://testhva.brightspace.com",
      "AppId": "app id",
      "AppKey": "app key",
      "UserId": "user id",
      "UserKey": "user key"
    },
  },
  "Jwt": {
    "Key": "some sufficiently log key to use for JWT signing"
  },
  "ConnectionString": "mongodb://examaple.com",
  "Teams": {
    "PRD": {
      "TenantId": "tenant id",
      "AppId": "service principal app id",
      "AppSecret": "corresponding secret"
    }
  }
}
```
