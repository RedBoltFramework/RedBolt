# Based on original work by David Manura
# Copyright (C) 2007-2012 LuaDist.
# Copyright (C) 2013 Brian Sidebotham
# Copyright (c) 2017-2022 the rbfx project.
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.

if(EMSCRIPTEN)
    return()
endif()

CPMAddPackage(
    NAME OpenSSL
    GIT_REPOSITORY https://github.com/openssl/openssl.git
    GIT_TAG OpenSSL_1_1_1q
    CUSTOM_CACHE_KEY "."
    GIT_SUBMODULES ":!*"
    DOWNLOAD_ONLY YES)

# Adapted from rbfx's CMake build for OpenSSL 1.1.1q.
set(REDBOLT_OPENSSL_SOURCE_DIR
    "${OpenSSL_SOURCE_DIR}"
    CACHE INTERNAL "OpenSSL source directory")
redbolt_require_path("${REDBOLT_OPENSSL_SOURCE_DIR}" "OpenSSL source tree")
set(_redbolt_openssl_binary_dir "${CMAKE_BINARY_DIR}/deps/OpenSSL")

include(CheckTypeSize)
check_type_size("long" LONG_INT)
check_type_size("long long" LONG_LONG_INT)
check_type_size("int" INT)
if("${LONG_INT}" EQUAL 8)
    set(SIXTY_FOUR_BIT_LONG ON)
elseif("${LONG_LONG_INT}" EQUAL 8)
    set(SIXTY_FOUR_BIT ON)
else()
    set(THIRTY_TWO_BIT ON)
endif()
if(MSVC OR (WIN32 AND MINGW AND NOT CYGWIN))
    set(OPENSSL_EXPORT_VAR_AS_FUNCTION 1)
endif()

function(redbolt_openssl_replace input match replacement output)
    string(FIND "${input}" "${match}" match_offset)
    if(match_offset EQUAL -1)
        message(FATAL_ERROR "OpenSSL template no longer contains an expected expression")
    endif()
    string(REPLACE "${match}" "${replacement}" result "${input}")
    set(${output} "${result}" PARENT_SCOPE)
endfunction()

function(redbolt_openssl_replace_block input begin end replacement output)
    string(FIND "${input}" "${begin}" begin_offset)
    if(begin_offset EQUAL -1)
        message(FATAL_ERROR "OpenSSL template no longer contains an expected block")
    endif()
    string(SUBSTRING "${input}" ${begin_offset} -1 tail)
    string(FIND "${tail}" "${end}" end_offset)
    if(end_offset EQUAL -1)
        message(FATAL_ERROR "OpenSSL template block has no expected end")
    endif()
    string(LENGTH "${end}" end_length)
    math(EXPR suffix_offset "${begin_offset} + ${end_offset} + ${end_length}")
    string(SUBSTRING "${input}" 0 ${begin_offset} prefix)
    string(SUBSTRING "${input}" ${suffix_offset} -1 suffix)
    set(${output} "${prefix}${replacement}${suffix}" PARENT_SCOPE)
endfunction()

set(_openssl_template_directory "${_redbolt_openssl_binary_dir}/templates")
set(_openssl_include_directory "${_redbolt_openssl_binary_dir}/include")

function(redbolt_configure_openssl_header path output)
    set(source "${REDBOLT_OPENSSL_SOURCE_DIR}/${path}")
    set(template "${_openssl_template_directory}/${path}")
    get_filename_component(template_directory "${template}" DIRECTORY)
    get_filename_component(output_directory "${output}" DIRECTORY)
    file(MAKE_DIRECTORY "${template_directory}" "${output_directory}")
    configure_file("${source}" "${template}" COPYONLY)
    file(READ "${template}" content)

    if(path STREQUAL "include/crypto/bn_conf.h.in")
        redbolt_openssl_replace("${content}"
            [=[{- join("\n",map { "/* $_ */" } @autowarntext) -}]=]
            "" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b64l} ? "#define" : "#undef" -} SIXTY_FOUR_BIT_LONG]=]
            "#cmakedefine SIXTY_FOUR_BIT_LONG" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b64}  ? "#define" : "#undef" -} SIXTY_FOUR_BIT]=]
            "#cmakedefine SIXTY_FOUR_BIT" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b32}  ? "#define" : "#undef" -} THIRTY_TWO_BIT]=]
            "#cmakedefine THIRTY_TWO_BIT" content)
    elseif(path STREQUAL "include/crypto/dso_conf.h.in")
        redbolt_openssl_replace("${content}"
            [=[{- join("\n",map { "/* $_ */" } @autowarntext) -}]=]
            "" content)
        redbolt_openssl_replace_block("${content}"
            [=[{-  # The DSO code]=]
            [=[# define DSO_EXTENSION "{- $target{dso_extension} -}"]=]
            "# define DSO_EXTENSION \".so\"" content)
    elseif(path STREQUAL "include/openssl/opensslconf.h.in")
        set(openssl_defines)
        foreach(define IN ITEMS
            OPENSSL_NO_MD2
            OPENSSL_NO_RC5
            OPENSSL_THREADS
            OPENSSL_RAND_SEED_OS
            OPENSSL_NO_ASAN
            OPENSSL_NO_ASM
            OPENSSL_NO_CRYPTO_MDEBUG
            OPENSSL_NO_CRYPTO_MDEBUG_BACKTRACE
            OPENSSL_NO_DEVCRYPTOENG
            OPENSSL_NO_EC_NISTP_64_GCC_128
            OPENSSL_NO_EGD
            OPENSSL_NO_EXTERNAL_TESTS
            OPENSSL_NO_FUZZ_AFL
            OPENSSL_NO_FUZZ_LIBFUZZER
            OPENSSL_NO_HEARTBEATS
            OPENSSL_NO_MSAN
            OPENSSL_NO_SCTP
            OPENSSL_NO_SSL_TRACE
            OPENSSL_NO_SSL3
            OPENSSL_NO_SSL3_METHOD
            OPENSSL_NO_UBSAN
            OPENSSL_NO_UNIT_TEST
            OPENSSL_NO_WEAK_SSL_CIPHERS
            OPENSSL_NO_STATIC_ENGINE
            OPENSSL_NO_AFALGENG)
            string(APPEND openssl_defines
                "#ifndef ${define}\n# define ${define}\n#endif\n")
        endforeach()
        redbolt_openssl_replace("${content}"
            [=[{- join("\n * ", @autowarntext) -}]=]
            "Generated by RedBolt's CMake adapter." content)
        redbolt_openssl_replace_block("${content}"
            [=[{- if (@{$config{openssl_sys_defines}}) {]=]
            [=[    "";
-}]=]
            "${openssl_defines}" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{processor} eq "386" ? "#define" : "#undef" -} I386_ONLY]=]
            "#undef I386_ONLY" content)
        redbolt_openssl_replace("${content}"
            [=[#define OPENSSL_UNISTD {- $target{unistd} -}]=]
            "#define OPENSSL_UNISTD <unistd.h>\n#define OPENSSLDIR \".\"\n#define ENGINESDIR \".\"\n#define DSO_NONE" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{export_var_as_fn} ? "#define" : "#undef" -} OPENSSL_EXPORT_VAR_AS_FUNCTION]=]
            "#cmakedefine OPENSSL_EXPORT_VAR_AS_FUNCTION" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{bn_ll} ? "# define" : "# undef" -} BN_LLONG]=]
            "#undef BN_LLONG" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b64l} ? "# define" : "# undef" -} SIXTY_FOUR_BIT_LONG]=]
            "#cmakedefine SIXTY_FOUR_BIT_LONG" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b64}  ? "# define" : "# undef" -} SIXTY_FOUR_BIT]=]
            "#cmakedefine SIXTY_FOUR_BIT" content)
        redbolt_openssl_replace("${content}"
            [=[{- $config{b32}  ? "# define" : "# undef" -} THIRTY_TWO_BIT]=]
            "#cmakedefine THIRTY_TWO_BIT" content)
        redbolt_openssl_replace("${content}"
            [=[#define RC4_INT {- $config{rc4_int} -}]=]
            "#define RC4_INT unsigned int" content)
    else()
        message(FATAL_ERROR "Unsupported OpenSSL header template: ${path}")
    endif()

    file(WRITE "${template}" "${content}")
    configure_file("${template}" "${output}" @ONLY)
endfunction()

redbolt_configure_openssl_header(
    include/openssl/opensslconf.h.in
    "${_openssl_include_directory}/openssl/opensslconf.h")
redbolt_configure_openssl_header(
    include/crypto/bn_conf.h.in
    "${_openssl_include_directory}/openssl/crypto/bn_conf.h")
redbolt_configure_openssl_header(
    include/crypto/dso_conf.h.in
    "${_openssl_include_directory}/openssl/crypto/dso_conf.h")
file(CONFIGURE
    OUTPUT "${_openssl_include_directory}/openssl/buildinf.h"
    CONTENT "#define DATE \"\"\n#define compiler_flags \"\"\n#define PLATFORM \"\"\n"
    @ONLY)
file(COPY "${REDBOLT_OPENSSL_SOURCE_DIR}/e_os.h"
    DESTINATION "${_openssl_include_directory}")

set(CRYPTO_SOURCE_FILES
    cpt_err.c cryptlib.c ctype.c cversion.c ebcdic.c ex_data.c getenv.c init.c mem.c mem_clr.c mem_dbg.c mem_sec.c o_dir.c o_fips.c o_fopen.c o_init.c o_str.c o_time.c threads_none.c threads_pthread.c threads_win.c uid.c
    aes/aes_cbc.c aes/aes_cfb.c aes/aes_core.c aes/aes_ecb.c aes/aes_ige.c aes/aes_misc.c aes/aes_ofb.c aes/aes_wrap.c
    aria/aria.c
    asn1/a_bitstr.c asn1/a_d2i_fp.c asn1/a_digest.c asn1/a_dup.c asn1/a_gentm.c asn1/a_i2d_fp.c asn1/a_int.c asn1/a_mbstr.c asn1/a_object.c asn1/a_octet.c asn1/a_print.c asn1/a_sign.c asn1/a_strex.c asn1/a_strnid.c asn1/a_time.c asn1/a_type.c asn1/a_utctm.c asn1/a_utf8.c asn1/a_verify.c asn1/ameth_lib.c asn1/asn1_err.c asn1/asn1_gen.c asn1/asn1_item_list.c asn1/asn1_lib.c asn1/asn1_par.c asn1/asn_mime.c asn1/asn_moid.c asn1/asn_mstbl.c asn1/asn_pack.c asn1/bio_asn1.c asn1/bio_ndef.c asn1/d2i_pr.c asn1/d2i_pu.c asn1/evp_asn1.c asn1/f_int.c asn1/f_string.c asn1/i2d_pr.c asn1/i2d_pu.c asn1/n_pkey.c asn1/nsseq.c asn1/p5_pbe.c asn1/p5_pbev2.c asn1/p5_scrypt.c asn1/p8_pkey.c asn1/t_bitst.c asn1/t_pkey.c asn1/t_spki.c asn1/tasn_dec.c asn1/tasn_enc.c asn1/tasn_fre.c asn1/tasn_new.c asn1/tasn_prn.c asn1/tasn_scn.c asn1/tasn_typ.c asn1/tasn_utl.c asn1/x_algor.c asn1/x_bignum.c asn1/x_info.c asn1/x_int64.c asn1/x_long.c asn1/x_pkey.c asn1/x_sig.c asn1/x_spki.c asn1/x_val.c
    async/async.c async/async_err.c async/async_wait.c
    async/arch/async_null.c async/arch/async_posix.c async/arch/async_win.c
    bf/bf_cfb64.c bf/bf_ecb.c bf/bf_enc.c bf/bf_ofb64.c bf/bf_skey.c
    bio/b_addr.c bio/b_dump.c bio/b_print.c bio/b_sock.c bio/b_sock2.c bio/bf_buff.c bio/bf_lbuf.c bio/bf_nbio.c bio/bf_null.c bio/bio_cb.c bio/bio_err.c bio/bio_lib.c bio/bio_meth.c bio/bss_acpt.c bio/bss_bio.c bio/bss_conn.c bio/bss_dgram.c bio/bss_fd.c bio/bss_file.c bio/bss_log.c bio/bss_mem.c bio/bss_null.c bio/bss_sock.c
    blake2/blake2b.c blake2/blake2s.c blake2/m_blake2b.c blake2/m_blake2s.c
    bn/bn_add.c bn/bn_asm.c bn/bn_blind.c bn/bn_const.c bn/bn_ctx.c bn/bn_depr.c bn/bn_dh.c bn/bn_div.c bn/bn_err.c bn/bn_exp.c bn/bn_exp2.c bn/bn_gcd.c bn/bn_gf2m.c bn/bn_intern.c bn/bn_kron.c bn/bn_lib.c bn/bn_mod.c bn/bn_mont.c bn/bn_mpi.c bn/bn_mul.c bn/bn_nist.c bn/bn_prime.c bn/bn_print.c bn/bn_rand.c bn/bn_recp.c bn/bn_shift.c bn/bn_sqr.c bn/bn_sqrt.c bn/bn_srp.c bn/bn_word.c bn/bn_x931p.c
    buffer/buf_err.c buffer/buffer.c
    camellia/camellia.c camellia/cmll_cbc.c camellia/cmll_cfb.c camellia/cmll_ctr.c camellia/cmll_ecb.c camellia/cmll_misc.c camellia/cmll_ofb.c
    cast/c_cfb64.c cast/c_ecb.c cast/c_enc.c cast/c_ofb64.c cast/c_skey.c
    chacha/chacha_enc.c
    cmac/cm_ameth.c cmac/cm_pmeth.c cmac/cmac.c
    cms/cms_asn1.c cms/cms_att.c cms/cms_cd.c cms/cms_dd.c cms/cms_enc.c cms/cms_env.c cms/cms_err.c cms/cms_ess.c cms/cms_io.c cms/cms_kari.c cms/cms_lib.c cms/cms_pwri.c cms/cms_sd.c cms/cms_smime.c
    comp/c_zlib.c comp/comp_err.c comp/comp_lib.c
    conf/conf_api.c conf/conf_def.c conf/conf_err.c conf/conf_lib.c conf/conf_mall.c conf/conf_mod.c conf/conf_sap.c conf/conf_ssl.c
    ct/ct_b64.c ct/ct_err.c ct/ct_log.c ct/ct_oct.c ct/ct_policy.c ct/ct_prn.c ct/ct_sct.c ct/ct_sct_ctx.c ct/ct_vfy.c ct/ct_x509v3.c
    des/cbc_cksm.c des/cbc_enc.c des/cfb64ede.c des/cfb64enc.c des/cfb_enc.c des/des_enc.c des/ecb3_enc.c des/ecb_enc.c des/fcrypt.c des/fcrypt_b.c des/ofb64ede.c des/ofb64enc.c des/ofb_enc.c des/pcbc_enc.c des/qud_cksm.c des/rand_key.c des/set_key.c des/str2key.c des/xcbc_enc.c
    dh/dh_ameth.c dh/dh_asn1.c dh/dh_check.c dh/dh_depr.c dh/dh_err.c dh/dh_gen.c dh/dh_kdf.c dh/dh_key.c dh/dh_lib.c dh/dh_meth.c dh/dh_pmeth.c dh/dh_prn.c dh/dh_rfc5114.c dh/dh_rfc7919.c
    dsa/dsa_ameth.c dsa/dsa_asn1.c dsa/dsa_depr.c dsa/dsa_err.c dsa/dsa_gen.c dsa/dsa_key.c dsa/dsa_lib.c dsa/dsa_meth.c dsa/dsa_ossl.c dsa/dsa_pmeth.c dsa/dsa_prn.c dsa/dsa_sign.c dsa/dsa_vrf.c
    dso/dso_dl.c dso/dso_dlfcn.c dso/dso_err.c dso/dso_lib.c dso/dso_openssl.c dso/dso_vms.c dso/dso_win32.c
    ec/curve25519.c ec/ec2_oct.c ec/ec2_smpl.c ec/ec_ameth.c ec/ec_asn1.c ec/ec_check.c ec/ec_curve.c ec/ec_cvt.c ec/ec_err.c ec/ec_key.c ec/ec_kmeth.c ec/ec_lib.c ec/ec_mult.c ec/ec_oct.c ec/ec_pmeth.c ec/ec_print.c ec/ecdh_kdf.c ec/ecdh_ossl.c ec/ecdsa_ossl.c ec/ecdsa_sign.c ec/ecdsa_vrf.c ec/eck_prn.c ec/ecp_mont.c ec/ecp_nist.c ec/ecp_nistp224.c ec/ecp_nistp256.c ec/ecp_nistp521.c ec/ecp_nistputil.c ec/ecp_oct.c ec/ecp_smpl.c ec/ecx_meth.c
    ec/curve448/curve448.c ec/curve448/curve448_tables.c ec/curve448/eddsa.c ec/curve448/f_generic.c ec/curve448/scalar.c
    ec/curve448/arch_32/f_impl.c
    engine/eng_all.c engine/eng_cnf.c engine/eng_ctrl.c engine/eng_dyn.c engine/eng_err.c engine/eng_fat.c engine/eng_init.c engine/eng_lib.c engine/eng_list.c engine/eng_openssl.c engine/eng_pkey.c engine/eng_rdrand.c engine/eng_table.c engine/tb_asnmth.c engine/tb_cipher.c engine/tb_dh.c engine/tb_digest.c engine/tb_dsa.c engine/tb_eckey.c engine/tb_pkmeth.c engine/tb_rand.c engine/tb_rsa.c
    err/err.c err/err_all.c err/err_prn.c
    evp/bio_b64.c evp/bio_enc.c evp/bio_md.c evp/bio_ok.c evp/c_allc.c evp/c_alld.c evp/cmeth_lib.c evp/digest.c evp/e_aes.c evp/e_aes_cbc_hmac_sha1.c evp/e_aes_cbc_hmac_sha256.c evp/e_aria.c evp/e_bf.c evp/e_camellia.c evp/e_cast.c evp/e_chacha20_poly1305.c evp/e_des.c evp/e_des3.c evp/e_idea.c evp/e_null.c evp/e_old.c evp/e_rc2.c evp/e_rc4.c evp/e_rc4_hmac_md5.c evp/e_rc5.c evp/e_seed.c evp/e_sm4.c evp/e_xcbc_d.c evp/encode.c evp/evp_cnf.c evp/evp_enc.c evp/evp_err.c evp/evp_key.c evp/evp_lib.c evp/evp_pbe.c evp/evp_pkey.c evp/m_md2.c evp/m_md4.c evp/m_md5.c evp/m_md5_sha1.c evp/m_mdc2.c evp/m_null.c evp/m_ripemd.c evp/m_sha1.c evp/m_sha3.c evp/m_sigver.c evp/m_wp.c evp/names.c evp/p5_crpt.c evp/p5_crpt2.c evp/p_dec.c evp/p_enc.c evp/p_lib.c evp/p_open.c evp/p_seal.c evp/p_sign.c evp/p_verify.c evp/pbe_scrypt.c evp/pmeth_fn.c evp/pmeth_gn.c evp/pmeth_lib.c
    hmac/hm_ameth.c hmac/hm_pmeth.c hmac/hmac.c
    idea/i_cbc.c idea/i_cfb64.c idea/i_ecb.c idea/i_ofb64.c idea/i_skey.c
    kdf/hkdf.c kdf/kdf_err.c kdf/scrypt.c kdf/tls1_prf.c
    lhash/lh_stats.c lhash/lhash.c
    md4/md4_dgst.c md4/md4_one.c
    md5/md5_dgst.c md5/md5_one.c
    mdc2/mdc2_one.c mdc2/mdc2dgst.c
    modes/cbc128.c modes/ccm128.c modes/cfb128.c modes/ctr128.c modes/cts128.c modes/gcm128.c modes/ocb128.c modes/ofb128.c modes/wrap128.c modes/xts128.c
    objects/o_names.c objects/obj_dat.c objects/obj_err.c objects/obj_lib.c objects/obj_xref.c
    ocsp/ocsp_asn.c ocsp/ocsp_cl.c ocsp/ocsp_err.c ocsp/ocsp_ext.c ocsp/ocsp_ht.c ocsp/ocsp_lib.c ocsp/ocsp_prn.c ocsp/ocsp_srv.c ocsp/ocsp_vfy.c ocsp/v3_ocsp.c
    pem/pem_all.c pem/pem_err.c pem/pem_info.c pem/pem_lib.c pem/pem_oth.c pem/pem_pk8.c pem/pem_pkey.c pem/pem_sign.c pem/pem_x509.c pem/pem_xaux.c pem/pvkfmt.c
    pkcs12/p12_add.c pkcs12/p12_asn.c pkcs12/p12_attr.c pkcs12/p12_crpt.c pkcs12/p12_crt.c pkcs12/p12_decr.c pkcs12/p12_init.c pkcs12/p12_key.c pkcs12/p12_kiss.c pkcs12/p12_mutl.c pkcs12/p12_npas.c pkcs12/p12_p8d.c pkcs12/p12_p8e.c pkcs12/p12_sbag.c pkcs12/p12_utl.c pkcs12/pk12err.c
    pkcs7/bio_pk7.c pkcs7/pk7_asn1.c pkcs7/pk7_attr.c pkcs7/pk7_doit.c pkcs7/pk7_lib.c pkcs7/pk7_mime.c pkcs7/pk7_smime.c pkcs7/pkcs7err.c
    poly1305/poly1305.c poly1305/poly1305_ameth.c poly1305/poly1305_pmeth.c
    rand/drbg_ctr.c rand/drbg_lib.c rand/rand_egd.c rand/rand_err.c rand/rand_lib.c rand/rand_unix.c rand/rand_vms.c rand/rand_win.c rand/randfile.c
    rc2/rc2_cbc.c rc2/rc2_ecb.c rc2/rc2_skey.c rc2/rc2cfb64.c rc2/rc2ofb64.c
    rc4/rc4_enc.c rc4/rc4_skey.c
    ripemd/rmd_dgst.c ripemd/rmd_one.c
    rsa/rsa_ameth.c rsa/rsa_asn1.c rsa/rsa_chk.c rsa/rsa_crpt.c rsa/rsa_depr.c rsa/rsa_err.c rsa/rsa_gen.c rsa/rsa_lib.c rsa/rsa_meth.c rsa/rsa_mp.c rsa/rsa_none.c rsa/rsa_oaep.c rsa/rsa_ossl.c rsa/rsa_pk1.c rsa/rsa_pmeth.c rsa/rsa_prn.c rsa/rsa_pss.c rsa/rsa_saos.c rsa/rsa_sign.c rsa/rsa_ssl.c rsa/rsa_x931.c rsa/rsa_x931g.c
    seed/seed.c seed/seed_cbc.c seed/seed_cfb.c seed/seed_ecb.c seed/seed_ofb.c
    sha/keccak1600.c sha/sha1_one.c sha/sha1dgst.c sha/sha256.c sha/sha512.c
    siphash/siphash.c siphash/siphash_ameth.c siphash/siphash_pmeth.c
    sm2/sm2_crypt.c sm2/sm2_err.c sm2/sm2_pmeth.c sm2/sm2_sign.c
    sm3/m_sm3.c sm3/sm3.c
    sm4/sm4.c
    srp/srp_lib.c srp/srp_vfy.c
    stack/stack.c
    store/loader_file.c store/store_err.c store/store_init.c store/store_lib.c store/store_register.c store/store_strings.c
    ts/ts_asn1.c ts/ts_conf.c ts/ts_err.c ts/ts_lib.c ts/ts_req_print.c ts/ts_req_utils.c ts/ts_rsp_print.c ts/ts_rsp_sign.c ts/ts_rsp_utils.c ts/ts_rsp_verify.c ts/ts_verify_ctx.c
    txt_db/txt_db.c
    ui/ui_err.c ui/ui_lib.c ui/ui_null.c ui/ui_openssl.c ui/ui_util.c
    whrlpool/wp_block.c whrlpool/wp_dgst.c
    x509/by_dir.c x509/by_file.c x509/t_crl.c x509/t_req.c x509/t_x509.c x509/x509_att.c x509/x509_cmp.c x509/x509_d2.c x509/x509_def.c x509/x509_err.c x509/x509_ext.c x509/x509_lu.c x509/x509_meth.c x509/x509_obj.c x509/x509_r2x.c x509/x509_req.c x509/x509_set.c x509/x509_trs.c x509/x509_txt.c x509/x509_v3.c x509/x509_vfy.c x509/x509_vpm.c x509/x509cset.c x509/x509name.c x509/x509rset.c x509/x509spki.c x509/x509type.c x509/x_all.c x509/x_attrib.c x509/x_crl.c x509/x_exten.c x509/x_name.c x509/x_pubkey.c x509/x_req.c x509/x_x509.c x509/x_x509a.c
    x509v3/pcy_cache.c x509v3/pcy_data.c x509v3/pcy_lib.c x509v3/pcy_map.c x509v3/pcy_node.c x509v3/pcy_tree.c x509v3/v3_addr.c x509v3/v3_admis.c x509v3/v3_akey.c x509v3/v3_akeya.c x509v3/v3_alt.c x509v3/v3_asid.c x509v3/v3_bcons.c x509v3/v3_bitst.c x509v3/v3_conf.c x509v3/v3_cpols.c x509v3/v3_crld.c x509v3/v3_enum.c x509v3/v3_extku.c x509v3/v3_genn.c x509v3/v3_ia5.c x509v3/v3_info.c x509v3/v3_int.c x509v3/v3_lib.c x509v3/v3_ncons.c x509v3/v3_pci.c x509v3/v3_pcia.c x509v3/v3_pcons.c x509v3/v3_pku.c x509v3/v3_pmaps.c x509v3/v3_prn.c x509v3/v3_purp.c x509v3/v3_skey.c x509v3/v3_sxnet.c x509v3/v3_tlsf.c x509v3/v3_utl.c x509v3/v3err.c)
list(TRANSFORM CRYPTO_SOURCE_FILES PREPEND "${REDBOLT_OPENSSL_SOURCE_DIR}/crypto/")

add_library(crypto STATIC ${CRYPTO_SOURCE_FILES})
target_include_directories(crypto
    PRIVATE
        "${REDBOLT_OPENSSL_SOURCE_DIR}/crypto/modes"
        "${REDBOLT_OPENSSL_SOURCE_DIR}/crypto/ec/curve448"
        "${REDBOLT_OPENSSL_SOURCE_DIR}/crypto/ec/curve448/arch_32"
        $<BUILD_INTERFACE:${_openssl_include_directory}/openssl/>
    PUBLIC
        $<BUILD_INTERFACE:${REDBOLT_OPENSSL_SOURCE_DIR}/include>
        $<BUILD_INTERFACE:${_openssl_include_directory}/>)

if(WIN32)
    target_compile_definitions(crypto PRIVATE WIN32_LEAN_AND_MEAN=1 _WINSOCK_DEPRECATED_NO_WARNINGS=1)
endif()
target_compile_definitions(crypto PRIVATE OPENSSL_NO_ASYNC=1 OPENSSL_SYS_WIN_CORE=1)

set(SSL_SOURCE_FILES
    bio_ssl.c d1_lib.c d1_msg.c d1_srtp.c methods.c packet.c pqueue.c s3_cbc.c s3_enc.c s3_lib.c s3_msg.c ssl_asn1.c ssl_cert.c ssl_ciph.c ssl_conf.c ssl_err.c ssl_init.c ssl_lib.c ssl_mcnf.c ssl_rsa.c ssl_sess.c ssl_stat.c ssl_txt.c ssl_utst.c t1_enc.c t1_lib.c t1_trce.c tls13_enc.c tls_srp.c packet.c tls13_enc.c
    record/dtls1_bitmap.c record/rec_layer_d1.c record/rec_layer_s3.c record/ssl3_buffer.c record/ssl3_record.c record/ssl3_record_tls13.c
    statem/extensions.c statem/extensions_clnt.c statem/extensions_cust.c statem/extensions_srvr.c statem/statem.c statem/statem_clnt.c statem/statem_dtls.c statem/statem_lib.c statem/statem_srvr.c)
list(TRANSFORM SSL_SOURCE_FILES PREPEND "${REDBOLT_OPENSSL_SOURCE_DIR}/ssl/")

add_library(ssl STATIC ${SSL_SOURCE_FILES})
target_link_libraries(ssl PUBLIC crypto)
set_target_properties(crypto ssl PROPERTIES EXCLUDE_FROM_ALL TRUE)
if(WIN32)
    target_compile_definitions(ssl PRIVATE WIN32_LEAN_AND_MEAN)
endif()
