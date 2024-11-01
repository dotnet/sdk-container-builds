# Using the NuGet package to do an end-to-end container build

This guidance will track the most up-to-date version of the package and tasks.
You should expect it to shrink noticeably over time!

## Prerequisites

* [.NET SDK 8.0.100](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
* Docker should be installed and running

## Usage

```bash
# create a new project and move to its directory
dotnet new web -n my-awesome-container-app
cd my-awesome-container-app

# publish your project
dotnet publish --os linux --arch x64 -t:PublishContainer

# run your app
docker run -it --rm -p 5010:8080 my-awesome-container-app:latest
```

Now you can go to `localhost:5010` and you should see the `Hello World!` text!
