
# QiWa.Common
Following the approach of Go, we define the underlying basic types in C#. This is a subproject of the QiWa(https://github.com/ahfuzhang/QiWa) project, making it easier for other dependencies to reference these basic definitions.

模仿 golang 的做法，在 csharp 中定义底层的基本类型。这是 QiWa(https://github.com/ahfuzhang/QiWa) 项目的子项目，便于其他依赖方可以引用到这些基本定义。

## 库的内容

* 目标：模仿 golang 的做法，把 golang 中优秀的设计引入 CSharp 中，从而提升 csharp 后端服务的性能。
* 包含内容:
  - `struct Error`: 模仿 golang 的 error，通过返回代表错误的信息，来代替 throw 语句。
  - `struct RentedBuffer`: 模仿 golang 的 []byte，提供一个自动扩容的数组，来简化各种数据序列化的操作。

## NuGet 仓库地址

https://www.nuget.org/packages/QiWa.Common/

```bash
dotnet add package QiWa.Common \
  --version 0.1.1 \
  --source https://api.nuget.org/v3/index.json
```

* 或者包含在 .csproj 文件中:

```xml
<ItemGroup>
  <PackageReference Include="QiWa.Common" Version="0.1.1" />
</ItemGroup>
```

## License

This project is licensed under the [MIT License](LICENSE).

Copyright (c) 2026 Fuchun Zhang

