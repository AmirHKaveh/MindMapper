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
**Object Mapping**

   Config of CustomMappingProfile file:
   ```
   public static class AppMappingProfile
   {
     public static CustomMappingProfile ConfigureMappings()
     {
         var profile = new CustomMappingProfile();
        
        profile.CreateMap<Model, ResponseModel>(x =>
         {
             x.ForMember((dest, val) => dest.CreateDate = val, src => src.CreateDate.ToShamsi());
         });
      }
   }
   ```

DI config in program.cs file:

```
 var profile = AppMappingProfile.ConfigureMappings();
 builder.Services.AddSingleton<IMappingProfile>(profile);
 builder.Services.AddSingleton<IMapper, Mapper>();
```
Use in contoller/page:

```
 private readonly IMapper _mapper;
 public ProductController(IMapper mapper)
 {
     _mapper = mapper;
 }
```

**Object Reverse Mapping**
   
```profile.CreateMap<RequestModel, Model>().ReverseMap();```

**Ignoring Properties**
   
``` profile.CreateMap<Model,RequestModel>().Ignore(x=>x.Name);```
               
 Map objects\
``` var model = Mapper.Map<Model>(request);```

 Config program.cs\
```var profile = AppMappingProfile.Register();```
```Mapper.Initialize(profile);```
