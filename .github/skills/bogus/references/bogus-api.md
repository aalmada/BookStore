# Bogus API Quick Reference

## Installation
- NuGet: `Install-Package Bogus`
- Add to .csproj:
```xml
<ItemGroup>
  <PackageReference Include="Bogus" Version="*" />
</ItemGroup>
```

## Basic Usage
```csharp
using Bogus;

var faker = new Faker<Person>()
    .RuleFor(p => p.Name, f => f.Name.FullName())
    .RuleFor(p => p.Email, f => f.Internet.Email());

var person = faker.Generate();
```

## Faker Facade
```csharp
var faker = new Bogus.Faker("en");
var order = new Order {
    OrderId = faker.Random.Number(1, 100),
    Item = faker.Lorem.Sentence(),
    Quantity = faker.Random.Number(1, 10)
};
```

## DataSets Directly
```csharp
var random = new Randomizer();
var lorem = new Lorem("en");
var order = new Order {
    OrderId = random.Number(1, 100),
    Item = lorem.Sentence(),
    Quantity = random.Number(1, 10)
};
```

## Locales
```csharp
var lorem = new Lorem(locale: "ko");
Console.WriteLine(lorem.Sentence(5));
```

## Analyzer Setup
```xml
<ItemGroup>
  <PackageReference Include="Bogus.Tools.Analyzer" Version="*" PrivateAssets="All"/>
</ItemGroup>
```
