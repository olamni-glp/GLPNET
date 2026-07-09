# Distributed under the OSI-approved BSD 3-Clause License.  See accompanying
# file LICENSE.rst or https://cmake.org/licensing for details.

cmake_minimum_required(VERSION ${CMAKE_VERSION}) # this file comes with cmake

# If CMAKE_DISABLE_SOURCE_CHANGES is set to true and the source directory is an
# existing directory in our source tree, calling file(MAKE_DIRECTORY) on it
# would cause a fatal error, even though it would be a no-op.
if(NOT EXISTS "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/msquic/submodules")
  file(MAKE_DIRECTORY "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/msquic/submodules")
endif()
file(MAKE_DIRECTORY
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-build"
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix"
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/tmp"
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/src/opensslquic-populate-stamp"
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/src"
  "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/src/opensslquic-populate-stamp"
)

set(configSubDirs )
foreach(subDir IN LISTS configSubDirs)
    file(MAKE_DIRECTORY "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/src/opensslquic-populate-stamp/${subDir}")
endforeach()
if(cfgdir)
  file(MAKE_DIRECTORY "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-subbuild/opensslquic-populate-prefix/src/opensslquic-populate-stamp${cfgdir}") # cfgdir has leading slash
endif()
