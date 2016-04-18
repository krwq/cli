@echo off
dotnet build && copy X:\oss\cli-2343\src\dotnet\bin\Debug\netcoreapp1.0\* X:\oss\cli-2343\artifacts\win10-x64\stage2\sdk\1.0.0-rc2-002417 /Y
