noinst_LTLIBRARIES = libunity-pal.la

AM_CFLAGS = $(WERROR_CFLAGS)

libunity_pal_la_SOURCES = \
	pal.cpp

libunity_pal_la_CFLAGS = -g -Wall -D_FORTIFY_SOURCE=2

AM_CPPFLAGS = -I$(srcdir) $(UNITY_PAL_CFLAGS)
AM_CXXFLAGS = -I$(srcdir) $(UNITY_PAL_CFLAGS)

