
COVERAGE_RAW    = build/coverage-raw
COVERAGE_REPORT = build/coverage-report

new:
	dotnet new sln -n "QiWa.Common"

test:
	dotnet test Tests/QiWa.Common.Tests/QiWa.Common.Tests.csproj --verbosity minimal

coverage:
	rm -rf $(COVERAGE_RAW) $(COVERAGE_REPORT)
	dotnet test Tests/QiWa.Common.Tests/QiWa.Common.Tests.csproj \
		--verbosity minimal \
		--collect:"XPlat Code Coverage" \
		--results-directory $(COVERAGE_RAW)
	which reportgenerator > /dev/null 2>&1 || \
		dotnet tool install --global dotnet-reportgenerator-globaltool
	reportgenerator \
		"-reports:$(COVERAGE_RAW)/**/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_REPORT)" \
		"-reporttypes:Html"
	@echo ""
	@echo "Coverage report: $(COVERAGE_REPORT)/index.html"

pack:
	dotnet pack -c Release -p:PackageVersion=0.1.5

# make push KEY=$(cat app_key.txt)
push:
	dotnet nuget push bin/Release/QiWa.Common.0.1.5.nupkg \
		--api-key $(KEY) \
		--source https://api.nuget.org/v3/index.json

