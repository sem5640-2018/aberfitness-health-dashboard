# health-dashboard
AberFitness Health Dashboard Microservice

| Branch | Status |
|-|-|
| Development | [![Development Build Status](https://travis-ci.org/sem5640-2018/health-dashboard.svg?branch=development)](https://travis-ci.org/sem5640-2018/health-dashboard) |
| Release | [![Release Build Status](https://travis-ci.org/sem5640-2018/health-dashboard.svg?branch=master)](https://travis-ci.org/sem5640-2018/health-dashboard) |

# Maintained by
* Charlie
* Andrew

# Objectives
* Pull data from the health-data-repository microservice
* Pull group data from the user-groups microservice
* Implements "goals" via the Challenge microservice API
* Administrative Interface for managing users
* Ability to manually import user data
* ~Must log audit trail data to GLaDOS~ No audit trail should be required.
* Has an interface for users to remove their data from the health-data-repository
* Displays current rankings

# Environment Variables

## Required Keys (All Environments)

| Environment Variable | Default | Description |
|-|-|-|
| ASPNETCORE_ENVIRONMENT | Production | Runtime environment, should be 'Development', 'Staging', or 'Production'. |
| Health_Dashboard__ClientId | N/A | Gatekeeper client ID. |
| Health_Dashboard__ClientSecret | N/A | Gatekeeper client secret. |
| Health_Dashboard__GatekeeperUrl | N/A | Gatekeeper OAuth authority URL. |
| Health_Dashboard__ChallengeUrl | N/A | Challenges URL. |
| Health_Dashboard__HealthDataRepositoryUrl | N/A | Health Data Repository URL. |
| Health_Dashboard__UserGroupsUrl (soon) | N/A | Health Data Repository URL. |


## Required Keys (Production + Staging Environments)
In addition to the above keys, you will also require:

| Environment Variable | Default | Description |
|-|-|-|
| Kestrel__Certificates__Default__Path | N/A | Path to the PFX certificate to use for HTTPS. |
| Kestrel__Certificates__Default__Password | N/A | Password for the HTTPS certificate. |
| Health_Dashboard__ReverseProxyHostname | http://nginx | The internal docker hostname of the reverse proxy being used. |
| Health_Dashboard__PathBase | /dashboard | The pathbase (name of the directory) that the app is being served from. |
