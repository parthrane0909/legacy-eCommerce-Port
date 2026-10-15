#!/usr/bin/env python3
"""
Summarises a Cobertura coverage report produced by `dotnet test --collect:"XPlat Code Coverage"`.

Reports overall line coverage and, separately, the figure the assignment cares about:
line coverage across the Core and Services/Infrastructure business layers.

Generated code (migrations, model snapshots, OpenAPI source generators) and framework
assemblies are excluded, because they are not hand-written application logic and would
otherwise drag the number down without telling us anything about test quality.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET
import collections

EXCLUDED_SUBSTRINGS = (
    "Migrations",
    "ModelSnapshot",
    "Generated",
    "CompilerServices",
    "Ecommerce.Tests",
)

CORE_PREFIX = "Ecommerce.Core"
SERVICES_PREFIX = "Ecommerce.Infrastructure"


def is_excluded(class_name: str) -> bool:
    return any(token in class_name for token in EXCLUDED_SUBSTRINGS)


def main() -> int:
    if len(sys.argv) > 1:
        reports = sys.argv[1:]
    else:
        # `dotnet test` writes results under the *test project's* directory
        # (Ecommerce.Tests/TestResults), not the solution root, so search recursively.
        here = os.path.dirname(os.path.abspath(__file__))
        backend_root = os.path.dirname(here)
        reports = sorted(
            glob.glob(
                os.path.join(backend_root, "**", "TestResults", "*", "coverage.cobertura.xml"),
                recursive=True,
            ),
            key=os.path.getmtime,
        )

    if not reports:
        print("No coverage report found. Run: dotnet test --collect:\"XPlat Code Coverage\"")
        return 1

    report = reports[-1]
    root = ET.parse(report).getroot()

    overall_lines = int(root.get("lines-valid", 0))
    overall_covered = int(root.get("lines-covered", 0))

    layers = collections.defaultdict(lambda: [0, 0])  # name -> [covered, valid]
    low = []

    # Cobertura groups by <package>, which carries the assembly name. Class names alone are
    # not reliably attributable (the writer emits bare type names), so walk packages.
    for package in root.iter("package"):
        package_name = package.get("name") or ""
        if is_excluded(package_name):
            continue

        if package_name.startswith(CORE_PREFIX):
            key = "Core (domain, DTOs, contracts)"
        elif package_name.startswith(SERVICES_PREFIX):
            key = "Services/Infrastructure (business logic)"
        else:
            continue

        for cls in package.iter("class"):
            name = cls.get("name") or ""
            if is_excluded(name):
                continue

            # This writer leaves the class-level counters and the <lines> summary attributes
            # empty, so count the individual <line hits="..."> elements instead.
            lines = cls.find("lines")
            line_elements = [] if lines is None else lines.findall("line")

            valid = len(line_elements)
            covered = sum(1 for line in line_elements if line.get("hits") != "0")
            if valid == 0:
                continue

            layers[key][0] += covered
            layers[key][1] += valid

            rate = covered / valid * 100
            if rate < 70:
                low.append((rate, name, covered, valid))

    def pct(pair):
        covered, valid = pair
        return (covered / valid * 100) if valid else 0.0

    print(f"Coverage report: {report}")
    print(f"Overall line coverage (all code):        {pct([overall_covered, overall_lines]):6.2f}%"
          f"   ({overall_covered}/{overall_lines})")

    core = layers["Core (domain, DTOs, contracts)"]
    services = layers["Services/Infrastructure (business logic)"]
    combined_covered = core[0] + services[0]
    combined_valid = core[1] + services[1]

    print()
    print("Assignment target: >= 70% line coverage on Core / Services")
    print(f"  Core:                                 {pct(core):6.2f}%   ({core[0]}/{core[1]})")
    print(f"  Services / Infrastructure:            {pct(services):6.2f}%   ({services[0]}/{services[1]})")
    combined = pct([combined_covered, combined_valid])
    print(f"  Combined Core + Services:             {combined:6.2f}%   "
          f"({combined_covered}/{combined_valid})")

    if low:
        print("\nClasses below 70% (candidates for more tests):")
        for rate, name, covered, valid in sorted(low)[:20]:
            short = name.rsplit('.', 2)[-1]
            print(f"  {rate:6.2f}%  {short:45} ({covered}/{valid})")

    print()
    if combined >= 70:
        print(f"PASS - Core / Services line coverage {combined:.2f}% meets the 70% target.")
        return 0

    print(f"FAIL - Core / Services line coverage {combined:.2f}% is below the 70% target.")
    return 2


if __name__ == "__main__":
    sys.exit(main())