# Introduction 
A set of utilities to automate EventGrid subscriptions provisioning in Azure Functions projects.

# Getting Started

This solution consists of 2 parts:
- `Bum.EventGrid.Subscriptions.Annotations` - the library containing attributes to decorate `EventGridTrigger`-ed functions.
    Currently there are 2 attributes `WebhookSubscription` and `FunctionAppSubscription`. 
    The first one should be used in cases when the destination is just an Http listener accepting HttpRequest. It's handy for local testing with ngrok.
    The second one should be used to decorate functions using eventgrid trigger.
- `Bum.EventGrid.Subscriptions.Scanner` - the console application that is able to scan solutions containing azure functions for an annotations.
    It's **improtant** to build the project/solution containing function apps first, as the tool works with dlls and also requires `function.json`
    to be present.

# Build and Test

## Build, pack and consume locally

- `Bum.EventGrid.Subscriptions.Annotations` steps:
    - navigate to project directory
    - execute `dotnet pack`. By default the package will be created in the `bin/Releace|Debug/netstandard2.1/` folder. 
      Note: to ensure that you will be using the fresh package after each build do nuget cache clear or increment a version.
    - Create a local nuget feed. It's a basic folder anywhere on you file system
    - execute `dotnet nuget add source <folder path> -n <preferred name>`
    - execute `dotnet nuget push -s <your local folder path> superfeed.eventgrid.annotations.<Version>.nupkg`
    - Install package to the required project as usual
- `Bum.EventGrid.Subscriptions.Scanner` steps:
    - navigate to project directory
    - execute `dotnet pack`
    - publish package to local nuget feed folder
    - navigate one level up of the project folder `cd ..`
    - execute `dotnet new tool-manifest`. It will create a `.config` folder and `dotent-tools.json` file in it.
    - execute `dotnet tool install superfeed.eventrgid.tool` it will install the tool locally and add the record in the `dotnet-tool.json` file.
    - Now you can execute `dotnet subb` command form the solution folder. To see options execute `dotnet subb --help`.

## Example usage

- Annotate the function
```c#
// The {environment} token is supported in string literals and will be raplaced by the tool using the supplied environment name argument
[FunctionAppSubscription(
Name = "TriggerSubscription",
FunctionAppName = "functionAppName", // currently this property supports {environment} argument, e.g my-function-app-{environment}
ResourceType = EventResourceType.Topic,
EventTypes = new[] {"type of event"},
Subject = "subject",
ResourceName = "your-event-grid-topic-{environment}")]

[FunctionName(nameof(Trigger))]
public Task Trigger([EventGridTrigger] EventGridEvent evt)
{
  
}
```

- Execute the tool

```powershell
dotnet bum -e staging -s <azure subscription id> -p <path to the solution or project folder> [optional -f *api*]
```
The command outputs the discovered projects and annotations as well as created subscriptions at the end.