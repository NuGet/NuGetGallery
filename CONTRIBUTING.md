# How to contribute

We welcome contributions to the NuGet Gallery! However, we do have a few suggestions for making sure we can integrate your code in the cleanest possible way.

## Getting Started
1. Make sure there is an issue in the [Issue Tracker](https://github.com/NuGet/NuGetGallery/issues) for the feature or bug you want to fix. If not, create one! 
  * **Pull Requests without an associated issue will not be accepted!**
1. Create a fork in GitHub
1. Create a branch off of the **dev-start** branch. Name it something which easily links it back to the issue. For example: "Bug-1234".
1. Make your changes
1. Send a Pull Request from your branch to the **dev** branch. **NOTE:** This is not the same branch you created your topic branch from!

If you mess up the branching or get confused, that's OK, we'd rather have your contribution than have you waste a lot of time figuring out our branches. However, using the right branches will help us immensely and will make it much easier for us to quickly accept your contribution!

## Some tips
* Please follow the code style of the rest of the gallery. When you can't figure out what the style for a particular aspect is, please bring that up in your PR and we'd be happy to advise you.
* Please don't surprise us with big Pull Requests. If you have a feature you want to work on, file an Issue so we can discuss it.
* Please DO grab things from the "Up For Grabs" milestone! Some of those things are small things we just don't have time to get to, but we'd be happy to take contributions. Think of it as a great way to get your feet wet :)
* However, don't feel obligated to stick to stuff in "Up For Grabs". We're happy to accept well-written, maintainable, PRs for any Issue in the Backlog.
* We have to dedicate resources to integrate and test your code, so it may take a while for us to integrate it. Feel free to ping the PR and nag us to take a look :)
* All contributions must include Unit Tests. We're also building out our Functional Tests, so the more of those you provide, the more excited we'll be to accept your change :).
* Contributed code must be free of compilation warnings, FxCop errors/warnings and Unit or Functional Test failures.
  * Please try to run the Functional Tests. Instructions to do so will be posted on our Wiki. If you can't run them, that's OK, but please let us know.
* Please remember that we will be maintaining the code you contribute, so there may be cases where we have to decline a feature because it doesn't make much sense in the main code. You are more than welcome to maintain a custom fork of the code for your use cases.

## Specific code style notes

**Note**: Even we don't always follow these in older code, but new code should follow them.

1. We use Allman style braces (the VS default)
1. We avoid "this." unless absolutely necessary
1. We always specify the visiblity, even if it's the default (i.e. "private string _foo" not "string _foo")
1. We avoid unnecessary abbreviations. Use 'Service' not 'Svc', etc.
1. We use "_camelCase" private members and use "readonly" where possible
1. Our tests use the [Arrange/Act/Assert](http://c2.com/cgi/wiki?ArrangeActAssert) pattern
1. Namespace Imports should be specified at the top of the file, OUTSIDE of "namespace" declarations and should be sorted alphabetically, with 'System.' namespaces at the top (there is a VS setting to do this):
1. Namespaces should, in general, match folder structure.
1. We use 4 spaces of indentation (in general, preserve indentation used by the file and always use spaces).

### Example File:

```C#
using System.Aardvarks;
using System.IO;
using System.Zebras;
using Aardvarks.CoolStuff;
using Zebra.Crossing;

namespace NuGetGallery.FolderName.SubFolderName 
{
    public class MyClass 
    {
        private readonly ISomethingService _something;
        
        public MyClass(ISomethingService something) 
        {
            _something = something;
        }
    }
}
```
