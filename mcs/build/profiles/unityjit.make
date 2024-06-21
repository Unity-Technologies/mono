# -*- makefile -*-

BOOTSTRAP_PROFILE = build

BOOTSTRAP_MCS = MONO_PATH="$(topdir)/class/lib/$(BOOTSTRAP_PROFILE)$(PLATFORM_PATH_SEPARATOR)$$MONO_PATH" $(INTERNAL_CSC)
MCS = $(BOOTSTRAP_MCS)

PLATFORMS = macos linux win32

profile-check:
	@:

DEFAULT_REFERENCES = mscorlib

ifeq ($(HOST_PLATFORM),win32)
	PLATFORM_FLAGS = -d:WIN_PLATFORM
endif

PROFILE_MCS_FLAGS = -d:NET_4_0 -d:NET_4_5 -d:NET_4_6 -d:MONO -d:UNITY_JIT -d:UNITY $(PLATFORM_FLAGS) -nowarn:1699 -nostdlib $(PLATFORM_DEBUG_FLAGS)
API_BIN_PROFILE = v4.7.1

FRAMEWORK_VERSION = 4.5
XBUILD_VERSION = 4.0

ifeq ($(HOST_PLATFORM),macos)
MONO_FEATURE_APPLETLS=1
ENABLE_GSS=1
PROFILE_MCS_FLAGS += -d:DISABLE_COM
endif

ifeq ($(HOST_PLATFORM),linux)
ENABLE_GSS=1
PROFILE_MCS_FLAGS += -d:DISABLE_COM
endif
