# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

# Reuse RedBolt's OpenSSL targets when dependencies call find_package(OpenSSL).
if(TARGET ssl AND NOT TARGET OpenSSL::SSL)
    add_library(OpenSSL::SSL ALIAS ssl)
endif()
if(TARGET crypto AND NOT TARGET OpenSSL::Crypto)
    add_library(OpenSSL::Crypto ALIAS crypto)
endif()
