# MCGalaxy build file
# Supports dotnet (6/8/9) and Mono (msbuild)
#
# Targets:
#   make           - build (auto-detects toolchain)
#   make build     - same as above
#   make run       - build then start the server
#   make clean     - remove build artifacts
#   make publish   - build self-contained single-file binary

# ── toolchain detection ────────────────────────────────────────────────────────

DOTNET  := $(shell command -v dotnet 2>/dev/null)
MSBUILD := $(shell command -v msbuild 2>/dev/null)

ifdef DOTNET
  # Pick the highest available major version we have a project file for
  DOTNET_VER := $(shell dotnet --version 2>/dev/null | cut -d. -f1)
  ifeq ($(DOTNET_VER),9)
    CLI_PROJ  = CLI/MCGalaxyCLI_dotnet9.csproj
    OUTDIR    = CLI/bin/Debug/net9.0
    TFM       = net9.0
  else ifeq ($(DOTNET_VER),8)
    CLI_PROJ  = CLI/MCGalaxyCLI_dotnet8.csproj
    OUTDIR    = CLI/bin/Debug/net8.0
    TFM       = net8.0
  else
    # default to 6 for anything older
    CLI_PROJ  = CLI/MCGalaxyCLI_dotnet6.csproj
    OUTDIR    = CLI/bin/Debug/net6.0
    TFM       = net6.0
  endif
  TOOLCHAIN = dotnet
else ifdef MSBUILD
  TOOLCHAIN = msbuild
  OUTDIR    = bin/Release
else
  TOOLCHAIN = none
endif

# ── main targets ──────────────────────────────────────────────────────────────

.PHONY: all build run clean publish check-toolchain

all: build

check-toolchain:
ifeq ($(TOOLCHAIN),none)
	$(error No build tool found. Install dotnet (https://dot.net) or mono-msbuild)
endif

build: check-toolchain
ifeq ($(TOOLCHAIN),dotnet)
	dotnet restore $(CLI_PROJ)
	dotnet build   $(CLI_PROJ) --no-restore
	@echo ""
	@echo "Build complete: $(OUTDIR)/MCGalaxyCLI"
else
	msbuild MCGalaxy.sln /p:Configuration=Release
	@echo ""
	@echo "Build complete: $(OUTDIR)/"
endif

run: build
ifeq ($(TOOLCHAIN),dotnet)
	cd $(OUTDIR) && ./MCGalaxyCLI
else
	cd $(OUTDIR) && mono MCGalaxyCLI.exe
endif

# Self-contained single binary (dotnet only)
publish: check-toolchain
ifeq ($(TOOLCHAIN),dotnet)
	dotnet publish CLI/MCGalaxyCLI_standalone6.csproj \
	  -r $(shell uname -s | tr '[:upper:]' '[:lower:]' | sed 's/darwin/osx/')-$(shell uname -m | sed 's/aarch64/arm64/;s/x86_64/x64/') \
	  --self-contained \
	  -o build/publish
	@echo ""
	@echo "Standalone binary: build/publish/MCGalaxyCLI"
else
	$(error publish target requires dotnet)
endif

clean:
ifdef DOTNET
	dotnet clean CLI/MCGalaxyCLI_dotnet6.csproj   2>/dev/null || true
	dotnet clean CLI/MCGalaxyCLI_dotnet8.csproj   2>/dev/null || true
	dotnet clean CLI/MCGalaxyCLI_dotnet9.csproj   2>/dev/null || true
endif
ifdef MSBUILD
	msbuild MCGalaxy.sln /t:Clean /p:Configuration=Release 2>/dev/null || true
	msbuild MCGalaxy.sln /t:Clean /p:Configuration=Debug  2>/dev/null || true
endif
	rm -rf CLI/bin CLI/obj MCGalaxy/bin MCGalaxy/obj bin obj build/publish
	@echo "Cleaned."

# ── info ──────────────────────────────────────────────────────────────────────

info:
	@echo "Toolchain : $(TOOLCHAIN)"
ifdef DOTNET
	@echo "dotnet    : $(shell dotnet --version)"
	@echo "Project   : $(CLI_PROJ)"
	@echo "Output    : $(OUTDIR)/MCGalaxyCLI"
endif
ifdef MSBUILD
	@echo "msbuild   : $(MSBUILD)"
	@echo "Output    : $(OUTDIR)/"
endif
