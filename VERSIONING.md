# Версионирование PandaPet

Проект использует [Semantic Versioning](https://semver.org/lang/ru/): `MAJOR.MINOR.PATCH`.

- `MAJOR` — несовместимые изменения настроек или поведения;
- `MINOR` — новые функции с сохранением совместимости;
- `PATCH` — исправления без изменения возможностей.

Единственный источник версии сборки — `VersionPrefix` в `Directory.Build.props`. Для предварительных выпусков используется `VersionSuffix`, например `beta.1`.

## Подготовка выпуска

```powershell
.\scripts\Set-Version.ps1 -Version 1.2.0
dotnet build .\PandaPet.slnx --configuration Release
git add .
git commit -m "Release 1.2.0"
git tag -a v1.2.0 -m "PandaPet 1.2.0"
git push origin main --follow-tags
```

Перед тегом нужно перенести изменения из раздела `Unreleased` файла `CHANGELOG.md` в раздел с номером и датой выпуска.
