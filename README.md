# MindMapper

🚀 A lightweight, high-performance object mapping library for .NET Core

Easily map objects, lists, and collections with support for reverse mapping, property ignoring, and custom value resolvers. Designed for simplicity and performance.

## Installation
Install via NuGet:

```dotnet add package MindMapper```

Or via Package Manager:

```Install-Package MindMapper```

## Features
✔ Simple object-to-object mapping\
✔ List/collection mapping\
✔ Reverse mapping (`Map` and `ReverseMap`)\
✔ Ignore properties during mapping\
✔ Fluent configuration API\
✔ High performance with expression trees


## Basic Usage
1. Object Mapping
   
   ```
    public static CustomMappingProfile Register()
     {
         var profile = new CustomMappingProfile();
        
        profile.CreateMap<Model, ResponseModel>(x =>
         {
             x.ForMember((dest, val) => dest.CreateDate = val, src => src.CreateDate.ToShamsi());
         });
   }


**2. Object Reverse Mapping**
   
```profile.CreateMap<RequestModel, Model>().ReverseMap();```

**4. Ignoring Properties**
   
``` profile.CreateMap<Model,RequestModel>().Ignore(x=>x.Name);```
               
 Map objects\
``` var model = Mapper.Map<Model>(request);```

 Config program.cs\
```var profile = AppMappingProfile.Register();```
```Mapper.Initialize(profile);```
