#! /usr/bin/env perl

package configdata;

use strict;
use warnings;

use Exporter;
#use vars qw(@ISA @EXPORT);
our @ISA = qw(Exporter);
our @EXPORT = qw(%config %target %disabled %withargs %unified_info @disablables);

our %config = (
  AR => "lib",
  ARFLAGS => [ "/nologo" ],
  AS => "nasm",
  ASFLAGS => [ "-g" ],
  CC => "cl",
  CFLAGS => [ "/W3 /wd4090 /nologo /O2", "/Qspectre" ],
  CPP => "\$(CC) /EP /C",
  CPPDEFINES => [  ],
  CPPFLAGS => [  ],
  CPPINCLUDES => [  ],
  CXX => "cl",
  CXXFLAGS => [ "/Qspectre" ],
  HASHBANGPERL => "/usr/bin/env perl",
  LD => "link",
  LDFLAGS => [ "/nologo /debug" ],
  LDLIBS => [  ],
  MT => "mt",
  MTFLAGS => [ "-nologo" ],
  PERL => "C:\\Users\\gavri\\tools\\strawberry\\perl\\bin\\perl.exe",
  RANLIB => "ranlib",
  RC => "rc",
  RCFLAGS => [  ],
  b32 => "0",
  b64 => "1",
  b64l => "0",
  bn_ll => "0",
  build_file => "makefile",
  build_file_templates => [ "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\Configurations\\common0.tmpl", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\Configurations\\windows-makefile.tmpl", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\Configurations\\common.tmpl" ],
  build_infos => [ "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\engines\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\test\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\tools\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\fuzz\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\hmac\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\buffer\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\stack\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\lhash\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\err\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\txt_db\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cmac\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\kdf\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\build.info", "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\test\\ossl_shim\\build.info" ],
  build_type => "release",
  builddir => ".",
  cflags => [  ],
  conf_files => [ "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\00-base-templates.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\10-main.conf" ],
  cppflags => [  ],
  cxxflags => [  ],
  defines => [ "NDEBUG" ],
  dirs => [ "crypto", "ssl", "engines", "apps", "test", "util", "tools", "fuzz" ],
  dso_defines => [ "PADLOCK_ASM" ],
  dynamic_engines => "0",
  engdirs => [  ],
  ex_libs => [  ],
  export_var_as_fn => "1",
  includes => [  ],
  lflags => [  ],
  lib_defines => [ "OPENSSL_PIC", "OPENSSL_CPUID_OBJ", "OPENSSL_IA32_SSE2", "OPENSSL_BN_ASM_MONT", "OPENSSL_BN_ASM_MONT5", "OPENSSL_BN_ASM_GF2m", "SHA1_ASM", "SHA256_ASM", "SHA512_ASM", "KECCAK1600_ASM", "RC4_ASM", "MD5_ASM", "AESNI_ASM", "VPAES_ASM", "GHASH_ASM", "ECP_NISTZ256_ASM", "X25519_ASM", "POLY1305_ASM" ],
  libdir => "",
  major => "1",
  minor => "1.1",
  openssl_algorithm_defines => [ "OPENSSL_NO_ARIA", "OPENSSL_NO_BF", "OPENSSL_NO_BLAKE2", "OPENSSL_NO_CAMELLIA", "OPENSSL_NO_CAST", "OPENSSL_NO_CMS", "OPENSSL_NO_COMP", "OPENSSL_NO_CT", "OPENSSL_NO_IDEA", "OPENSSL_NO_MD2", "OPENSSL_NO_MD4", "OPENSSL_NO_MDC2", "OPENSSL_NO_RC2", "OPENSSL_NO_RC4", "OPENSSL_NO_RC5", "OPENSSL_NO_RMD160", "OPENSSL_NO_SEED", "OPENSSL_NO_SIPHASH", "OPENSSL_NO_SM2", "OPENSSL_NO_SM3", "OPENSSL_NO_SM4", "OPENSSL_NO_SRP", "OPENSSL_NO_TS", "OPENSSL_NO_WHIRLPOOL" ],
  openssl_api_defines => [  ],
  openssl_other_defines => [ "OPENSSL_RAND_SEED_OS", "OPENSSL_NO_AFALGENG", "OPENSSL_NO_ASAN", "OPENSSL_NO_CRYPTO_MDEBUG", "OPENSSL_NO_CRYPTO_MDEBUG_BACKTRACE", "OPENSSL_NO_DEVCRYPTOENG", "OPENSSL_NO_DGRAM", "OPENSSL_NO_DSO", "OPENSSL_NO_DTLS", "OPENSSL_NO_DTLS1", "OPENSSL_NO_DTLS1_METHOD", "OPENSSL_NO_DTLS1_2", "OPENSSL_NO_DTLS1_2_METHOD", "OPENSSL_NO_EC2M", "OPENSSL_NO_EC_NISTP_64_GCC_128", "OPENSSL_NO_EGD", "OPENSSL_NO_EXTERNAL_TESTS", "OPENSSL_NO_FUZZ_AFL", "OPENSSL_NO_FUZZ_LIBFUZZER", "OPENSSL_NO_GOST", "OPENSSL_NO_HEARTBEATS", "OPENSSL_NO_MSAN", "OPENSSL_NO_OCB", "OPENSSL_NO_PSK", "OPENSSL_NO_SCRYPT", "OPENSSL_NO_SCTP", "OPENSSL_NO_SRTP", "OPENSSL_NO_SSL_TRACE", "OPENSSL_NO_SSL3", "OPENSSL_NO_SSL3_METHOD", "OPENSSL_NO_TESTS", "OPENSSL_NO_TLS1", "OPENSSL_NO_TLS1_METHOD", "OPENSSL_NO_TLS1_1", "OPENSSL_NO_TLS1_1_METHOD", "OPENSSL_NO_TLS1_2", "OPENSSL_NO_TLS1_2_METHOD", "OPENSSL_NO_UBSAN", "OPENSSL_NO_UNIT_TEST", "OPENSSL_NO_WEAK_SSL_CIPHERS", "OPENSSL_NO_DYNAMIC_ENGINE" ],
  openssl_sys_defines => [ "OPENSSL_SYS_WIN64A" ],
  openssl_thread_defines => [ "OPENSSL_THREADS" ],
  openssldir => "C:\\Program Files\\Common Files\\SSL",
  options => "enable-tls1_3 --openssldir=C:\\Program Files\\Common Files\\SSL /Qspectre --prefix=D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-build/openssl/release no-afalgeng no-aria no-asan no-bf no-blake2 no-buildtest-c++ no-camellia no-cast no-cms no-comp no-crypto-mdebug no-crypto-mdebug-backtrace no-ct no-devcryptoeng no-dgram no-dso no-dtls no-dtls1 no-dtls1-method no-dtls1_2 no-dtls1_2-method no-dynamic-engine no-ec2m no-ec_nistp_64_gcc_128 no-egd no-external-tests no-fuzz-afl no-fuzz-libfuzzer no-gost no-heartbeats no-idea no-makedepend no-md2 no-md4 no-mdc2 no-msan no-ocb no-psk no-rc2 no-rc4 no-rc5 no-rmd160 no-scrypt no-sctp no-seed no-shared no-siphash no-sm2 no-sm3 no-sm4 no-srp no-srtp no-ssl-trace no-ssl3 no-ssl3-method no-tests no-tls1 no-tls1-method no-tls1_1 no-tls1_1-method no-tls1_2 no-tls1_2-method no-ts no-ubsan no-unit-test no-weak-ssl-ciphers no-whirlpool no-zlib no-zlib-dynamic",
  perl_archname => "MSWin32-x64-multi-thread",
  perl_cmd => "C:\\Users\\gavri\\tools\\strawberry\\perl\\bin\\perl.exe",
  perl_version => "5.42.2",
  perlargv => [ "enable-tls1_3", "no-makedepend", "no-dgram", "no-ssl3", "no-psk", "no-srp", "no-zlib", "no-egd", "no-idea", "no-rc5", "no-rc4", "no-afalgeng", "no-comp", "no-cms", "no-ct", "no-srp", "no-srtp", "no-ts", "no-gost", "no-dso", "no-ec2m", "no-tls1", "no-tls1_1", "no-tls1_2", "no-dtls", "no-dtls1", "no-dtls1_2", "no-ssl", "no-ssl3-method", "no-tls1-method", "no-tls1_1-method", "no-tls1_2-method", "no-dtls1-method", "no-dtls1_2-method", "no-siphash", "no-whirlpool", "no-aria", "no-bf", "no-blake2", "no-sm2", "no-sm3", "no-sm4", "no-camellia", "no-cast", "no-md4", "no-mdc2", "no-ocb", "no-rc2", "no-rmd160", "no-scrypt", "no-seed", "no-weak-ssl-ciphers", "no-shared", "no-tests", "--openssldir=C:\\Program Files\\Common Files\\SSL", "VC-WIN64A", "/Qspectre", "--prefix=D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-build/openssl/release" ],
  perlenv => {
      "AR" => undef,
      "BUILDFILE" => undef,
      "CC" => "cl",
      "CFLAGS" => undef,
      "CPPFLAGS" => undef,
      "CROSS_COMPILE" => undef,
      "CXX" => "cl",
      "CXXFLAGS" => undef,
      "HASHBANGPERL" => undef,
      "LDFLAGS" => undef,
      "LDLIBS" => undef,
      "OPENSSL_LOCAL_CONFIG_DIR" => undef,
      "PERL" => undef,
      "RANLIB" => undef,
      "RC" => undef,
      "RCFLAGS" => undef,
      "WINDRES" => undef,
      "__CNF_CFLAGS" => undef,
      "__CNF_CPPDEFINES" => undef,
      "__CNF_CPPFLAGS" => undef,
      "__CNF_CPPINCLUDES" => undef,
      "__CNF_CXXFLAGS" => undef,
      "__CNF_LDFLAGS" => undef,
      "__CNF_LDLIBS" => undef,
  },
  prefix => "D:/BSTDEV/research/GLP/GLPNET/gleam_quic/profile_c/_build/default/lib/quicer/c_build_win/_deps/opensslquic-build/openssl/release",
  processor => "",
  rc4_int => "unsigned int",
  sdirs => [ "objects", "md5", "sha", "hmac", "poly1305", "des", "aes", "chacha", "modes", "bn", "ec", "rsa", "dsa", "dh", "dso", "engine", "buffer", "bio", "stack", "lhash", "rand", "err", "evp", "asn1", "pem", "x509", "x509v3", "conf", "txt_db", "pkcs7", "pkcs12", "ocsp", "ui", "cmac", "async", "kdf", "store" ],
  shlib_major => "81",
  shlib_minor => "1.1",
  shlib_version_history => "",
  shlib_version_number => "81.1.1",
  sourcedir => "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
  target => "VC-WIN64A",
  tdirs => [ "ossl_shim" ],
  version => "1.1.1w+quic",
  version_num => "0x1010117fL",
);

our %target = (
  AR => "lib",
  ARFLAGS => "/nologo",
  AS => "nasm",
  ASFLAGS => "-g",
  CC => "cl",
  CFLAGS => "/W3 /wd4090 /nologo /O2",
  CPP => "\$(CC) /EP /C",
  HASHBANGPERL => "/usr/bin/env perl",
  LD => "link",
  LDFLAGS => "/nologo /debug",
  MT => "mt",
  MTFLAGS => "-nologo",
  RANLIB => "CODE(0x25c4a08b2f0)",
  RC => "rc",
  _conf_fname_int => [ "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\00-base-templates.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\00-base-templates.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\10-main.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\10-main.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\10-main.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\00-base-templates.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\10-main.conf", "D:\\BSTDEV\\research\\GLP\\GLPNET\\gleam_quic\\profile_c\\_build\\default\\lib\\quicer\\msquic\\submodules\\openssl\\Configurations\\shared-info.pl" ],
  aes_asm_src => "aes_core.c aes_cbc.c vpaes-x86_64.s aesni-x86_64.s aesni-sha1-x86_64.s aesni-sha256-x86_64.s aesni-mb-x86_64.s",
  aes_obj => "aes_core.o aes_cbc.o vpaes-x86_64.o aesni-x86_64.o aesni-sha1-x86_64.o aesni-sha256-x86_64.o aesni-mb-x86_64.o",
  apps_aux_src => "win32_init.c",
  apps_init_src => "",
  apps_obj => "win32_init.o",
  aroutflag => "/out:",
  asflags => "-Ox -f win64 -DNEAR",
  asoutflag => "-o ",
  bf_asm_src => "bf_enc.c",
  bf_obj => "bf_enc.o",
  bin_cflags => "/Zi /Fdapp.pdb /MT",
  bin_lflags => "/subsystem:console /opt:ref",
  bn_asm_src => "bn_asm.c x86_64-mont.s x86_64-mont5.s x86_64-gf2m.s rsaz_exp.c rsaz-x86_64.s rsaz-avx2.s",
  bn_obj => "bn_asm.o x86_64-mont.o x86_64-mont5.o x86_64-gf2m.o rsaz_exp.o rsaz-x86_64.o rsaz-avx2.o",
  bn_ops => "EXPORT_VAR_AS_FN SIXTY_FOUR_BIT",
  build_file => "makefile",
  build_scheme => [ "unified", "windows", "VC-common" ],
  cast_asm_src => "c_enc.c",
  cast_obj => "c_enc.o",
  cflags => "/Gs0 /GF /Gy",
  chacha_asm_src => "chacha-x86_64.s",
  chacha_obj => "chacha-x86_64.o",
  cmll_asm_src => "cmll-x86_64.s cmll_misc.c",
  cmll_obj => "cmll-x86_64.o cmll_misc.o",
  coutflag => "/Fo",
  cppflags => "",
  cpuid_asm_src => "x86_64cpuid.s",
  cpuid_obj => "x86_64cpuid.o",
  defines => [ "OPENSSL_SYS_WIN32", "WIN32_LEAN_AND_MEAN", "UNICODE", "_UNICODE", "_CRT_SECURE_NO_DEPRECATE", "_WINSOCK_DEPRECATED_NO_WARNINGS" ],
  des_asm_src => "des_enc.c fcrypt_b.c",
  des_obj => "des_enc.o fcrypt_b.o",
  disable => [  ],
  dso_cflags => "/Zi /Fddso.pdb",
  dso_extension => ".dll",
  dso_scheme => "win32",
  ec_asm_src => "ecp_nistz256.c ecp_nistz256-x86_64.s x25519-x86_64.s",
  ec_obj => "ecp_nistz256.o ecp_nistz256-x86_64.o x25519-x86_64.o",
  enable => [  ],
  ex_libs => "ws2_32.lib gdi32.lib advapi32.lib crypt32.lib user32.lib",
  exe_extension => "",
  includes => [  ],
  keccak1600_asm_src => "keccak1600-x86_64.s",
  keccak1600_obj => "keccak1600-x86_64.o",
  ldoutflag => "/out:",
  lflags => "",
  lib_cflags => "/Zi /Fdossl_static.pdb /MT /Zl",
  lib_cppflags => "",
  lib_defines => [ "L_ENDIAN" ],
  md5_asm_src => "md5-x86_64.s",
  md5_obj => "md5-x86_64.o",
  modes_asm_src => "ghash-x86_64.s aesni-gcm-x86_64.s",
  modes_obj => "ghash-x86_64.o aesni-gcm-x86_64.o",
  module_cflags => "",
  module_cxxflags => "",
  module_ldflags => "/dll",
  mtinflag => "-manifest ",
  mtoutflag => "-outputresource:",
  multilib => "-x64",
  padlock_asm_src => "e_padlock-x86_64.s",
  padlock_obj => "e_padlock-x86_64.o",
  perlasm_scheme => "auto",
  poly1305_asm_src => "poly1305-x86_64.s",
  poly1305_obj => "poly1305-x86_64.o",
  rc4_asm_src => "rc4-x86_64.s rc4-md5-x86_64.s",
  rc4_obj => "rc4-x86_64.o rc4-md5-x86_64.o",
  rc5_asm_src => "rc5_enc.c",
  rc5_obj => "rc5_enc.o",
  rcoutflag => "/fo",
  rmd160_asm_src => "",
  rmd160_obj => "",
  sha1_asm_src => "sha1-x86_64.s sha256-x86_64.s sha512-x86_64.s sha1-mb-x86_64.s sha256-mb-x86_64.s",
  sha1_obj => "sha1-x86_64.o sha256-x86_64.o sha512-x86_64.o sha1-mb-x86_64.o sha256-mb-x86_64.o",
  shared_cflag => "",
  shared_defines => [  ],
  shared_extension => ".dll",
  shared_extension_simple => ".dll",
  shared_ldflag => "/dll",
  shared_rcflag => "",
  shared_target => "win-shared",
  sys_id => "WIN64A",
  template => "1",
  thread_defines => [  ],
  thread_scheme => "winthreads",
  unistd => "<unistd.h>",
  uplink_aux_src => "",
  uplink_obj => "",
  wp_asm_src => "wp-x86_64.s",
  wp_obj => "wp-x86_64.o",
);

our %available_protocols = (
  tls => [ "ssl3", "tls1", "tls1_1", "tls1_2", "tls1_3" ],
  dtls => [ "dtls1", "dtls1_2" ],
);

our @disablables = (
  "afalgeng",
  "aria",
  "asan",
  "asm",
  "async",
  "autoalginit",
  "autoerrinit",
  "autoload-config",
  "bf",
  "blake2",
  "buildtest-c\\+\\+",
  "camellia",
  "capieng",
  "cast",
  "chacha",
  "cmac",
  "cms",
  "comp",
  "crypto-mdebug",
  "crypto-mdebug-backtrace",
  "ct",
  "deprecated",
  "des",
  "devcryptoeng",
  "dgram",
  "dh",
  "dsa",
  "dso",
  "dtls",
  "dynamic-engine",
  "ec",
  "ec2m",
  "ecdh",
  "ecdsa",
  "ec_nistp_64_gcc_128",
  "egd",
  "engine",
  "err",
  "external-tests",
  "filenames",
  "fuzz-libfuzzer",
  "fuzz-afl",
  "gost",
  "heartbeats",
  "hw(-.+)?",
  "idea",
  "makedepend",
  "md2",
  "md4",
  "mdc2",
  "msan",
  "multiblock",
  "nextprotoneg",
  "pinshared",
  "ocb",
  "ocsp",
  "pic",
  "poly1305",
  "posix-io",
  "psk",
  "quic",
  "rc2",
  "rc4",
  "rc5",
  "rdrand",
  "rfc3779",
  "rmd160",
  "scrypt",
  "sctp",
  "seed",
  "shared",
  "siphash",
  "sm2",
  "sm3",
  "sm4",
  "sock",
  "srp",
  "srtp",
  "sse2",
  "ssl",
  "ssl-trace",
  "static-engine",
  "stdio",
  "tests",
  "threads",
  "tls",
  "ts",
  "ubsan",
  "ui-console",
  "unit-test",
  "whirlpool",
  "weak-ssl-ciphers",
  "zlib",
  "zlib-dynamic",
  "ssl3",
  "ssl3-method",
  "tls1",
  "tls1-method",
  "tls1_1",
  "tls1_1-method",
  "tls1_2",
  "tls1_2-method",
  "tls1_3",
  "dtls1",
  "dtls1-method",
  "dtls1_2",
  "dtls1_2-method",
);

our %disabled = (
  "afalgeng" => "option",
  "aria" => "option",
  "asan" => "default",
  "bf" => "option",
  "blake2" => "option",
  "buildtest-c++" => "default",
  "camellia" => "option",
  "cast" => "option",
  "cms" => "option",
  "comp" => "option",
  "crypto-mdebug" => "default",
  "crypto-mdebug-backtrace" => "default",
  "ct" => "option",
  "devcryptoeng" => "default",
  "dgram" => "option",
  "dso" => "option",
  "dtls" => "option(dtls)",
  "dtls1" => "option(dtls1-method)",
  "dtls1-method" => "option",
  "dtls1_2" => "option(dtls1_2-method)",
  "dtls1_2-method" => "option",
  "dynamic-engine" => "cascade",
  "ec2m" => "option",
  "ec_nistp_64_gcc_128" => "default",
  "egd" => "option",
  "external-tests" => "default",
  "fuzz-afl" => "default",
  "fuzz-libfuzzer" => "default",
  "gost" => "option",
  "heartbeats" => "default",
  "idea" => "option",
  "makedepend" => "option",
  "md2" => "default",
  "md4" => "option",
  "mdc2" => "option",
  "msan" => "default",
  "ocb" => "option",
  "psk" => "option",
  "rc2" => "option",
  "rc4" => "option",
  "rc5" => "option",
  "rmd160" => "option",
  "scrypt" => "option",
  "sctp" => "default",
  "seed" => "option",
  "shared" => "option",
  "siphash" => "option",
  "sm2" => "option",
  "sm3" => "option",
  "sm4" => "option",
  "srp" => "option",
  "srtp" => "option",
  "ssl-trace" => "default",
  "ssl3" => "option(ssl3-method)",
  "ssl3-method" => "option",
  "tests" => "option",
  "tls1" => "option(tls1-method)",
  "tls1-method" => "option",
  "tls1_1" => "option(tls1_1-method)",
  "tls1_1-method" => "option",
  "tls1_2" => "option(tls1_2-method)",
  "tls1_2-method" => "option",
  "ts" => "option",
  "ubsan" => "default",
  "unit-test" => "default",
  "weak-ssl-ciphers" => "option",
  "whirlpool" => "option",
  "zlib" => "option",
  "zlib-dynamic" => "default",
);

our %withargs = (
);

our %unified_info = (
    "depends" =>
        {
            "" =>
                [
                    "include\\crypto\\bn_conf.h",
                    "include\\crypto\\dso_conf.h",
                    "include\\openssl\\opensslconf.h",
                ],
            "apps\\asn1pars.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\ca.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\ciphers.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\cms.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\crl.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\crl2p7.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\dgst.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\dhparam.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\dsa.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\dsaparam.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\ec.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\ecparam.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\enc.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\engine.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\errstr.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\gendsa.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\genpkey.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\genrsa.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\nseq.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\ocsp.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\openssl" =>
                [
                    "apps\\libapps.a",
                    "libssl",
                ],
            "apps\\openssl.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\passwd.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkcs12.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkcs7.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkcs8.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkey.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkeyparam.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\pkeyutl.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\prime.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\progs.h" =>
                [
                    "configdata.pm",
                ],
            "apps\\rand.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\rehash.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\req.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\rsa.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\rsautl.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\s_client.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\s_server.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\s_time.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\sess_id.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\smime.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\speed.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\spkac.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\storeutl.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\verify.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\version.o" =>
                [
                    "apps\\progs.h",
                ],
            "apps\\x509.o" =>
                [
                    "apps\\progs.h",
                ],
            "crypto\\aes\\aes-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\aes\\aesni-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\aes\\aest4-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\sparcv9_modes.pl",
                ],
            "crypto\\aes\\vpaes-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\bn\\bn-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\bn\\co-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\bn\\x86-gf2m.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\bn\\x86-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\buildinf.h" =>
                [
                    "configdata.pm",
                ],
            "crypto\\cversion.o" =>
                [
                    "crypto\\buildinf.h",
                ],
            "crypto\\des\\crypt586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\cbc.pl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\des\\des-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\cbc.pl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\sha\\sha1-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\sha\\sha256-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\sha\\sha512-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "crypto\\x86cpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\perlasm\\x86asm.pl",
                ],
            "include\\crypto\\bn_conf.h" =>
                [
                    "configdata.pm",
                ],
            "include\\crypto\\dso_conf.h" =>
                [
                    "configdata.pm",
                ],
            "include\\openssl\\opensslconf.h" =>
                [
                    "configdata.pm",
                ],
            "libcrypto.def" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\libcrypto.num",
                ],
            "libssl" =>
                [
                    "libcrypto",
                ],
            "libssl.def" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\libssl.num",
                ],
        },
    "dirinfo" =>
        {
            "apps" =>
                {
                    "products" =>
                        {
                            "bin" =>
                                [
                                    "apps\\openssl",
                                ],
                            "lib" =>
                                [
                                    "apps\\libapps.a",
                                ],
                        },
                },
            "crypto" =>
                {
                    "deps" =>
                        [
                            "crypto\\cpt_err.o",
                            "crypto\\cryptlib.o",
                            "crypto\\ctype.o",
                            "crypto\\cversion.o",
                            "crypto\\ebcdic.o",
                            "crypto\\ex_data.o",
                            "crypto\\getenv.o",
                            "crypto\\init.o",
                            "crypto\\mem.o",
                            "crypto\\mem_dbg.o",
                            "crypto\\mem_sec.o",
                            "crypto\\o_dir.o",
                            "crypto\\o_fips.o",
                            "crypto\\o_fopen.o",
                            "crypto\\o_init.o",
                            "crypto\\o_str.o",
                            "crypto\\o_time.o",
                            "crypto\\threads_none.o",
                            "crypto\\threads_pthread.o",
                            "crypto\\threads_win.o",
                            "crypto\\uid.o",
                            "crypto\\x86_64cpuid.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\aes" =>
                {
                    "deps" =>
                        [
                            "crypto\\aes\\aes_cbc.o",
                            "crypto\\aes\\aes_cfb.o",
                            "crypto\\aes\\aes_core.o",
                            "crypto\\aes\\aes_ecb.o",
                            "crypto\\aes\\aes_ige.o",
                            "crypto\\aes\\aes_misc.o",
                            "crypto\\aes\\aes_ofb.o",
                            "crypto\\aes\\aes_wrap.o",
                            "crypto\\aes\\aesni-mb-x86_64.o",
                            "crypto\\aes\\aesni-sha1-x86_64.o",
                            "crypto\\aes\\aesni-sha256-x86_64.o",
                            "crypto\\aes\\aesni-x86_64.o",
                            "crypto\\aes\\vpaes-x86_64.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\asn1" =>
                {
                    "deps" =>
                        [
                            "crypto\\asn1\\a_bitstr.o",
                            "crypto\\asn1\\a_d2i_fp.o",
                            "crypto\\asn1\\a_digest.o",
                            "crypto\\asn1\\a_dup.o",
                            "crypto\\asn1\\a_gentm.o",
                            "crypto\\asn1\\a_i2d_fp.o",
                            "crypto\\asn1\\a_int.o",
                            "crypto\\asn1\\a_mbstr.o",
                            "crypto\\asn1\\a_object.o",
                            "crypto\\asn1\\a_octet.o",
                            "crypto\\asn1\\a_print.o",
                            "crypto\\asn1\\a_sign.o",
                            "crypto\\asn1\\a_strex.o",
                            "crypto\\asn1\\a_strnid.o",
                            "crypto\\asn1\\a_time.o",
                            "crypto\\asn1\\a_type.o",
                            "crypto\\asn1\\a_utctm.o",
                            "crypto\\asn1\\a_utf8.o",
                            "crypto\\asn1\\a_verify.o",
                            "crypto\\asn1\\ameth_lib.o",
                            "crypto\\asn1\\asn1_err.o",
                            "crypto\\asn1\\asn1_gen.o",
                            "crypto\\asn1\\asn1_item_list.o",
                            "crypto\\asn1\\asn1_lib.o",
                            "crypto\\asn1\\asn1_par.o",
                            "crypto\\asn1\\asn_mime.o",
                            "crypto\\asn1\\asn_moid.o",
                            "crypto\\asn1\\asn_mstbl.o",
                            "crypto\\asn1\\asn_pack.o",
                            "crypto\\asn1\\bio_asn1.o",
                            "crypto\\asn1\\bio_ndef.o",
                            "crypto\\asn1\\d2i_pr.o",
                            "crypto\\asn1\\d2i_pu.o",
                            "crypto\\asn1\\evp_asn1.o",
                            "crypto\\asn1\\f_int.o",
                            "crypto\\asn1\\f_string.o",
                            "crypto\\asn1\\i2d_pr.o",
                            "crypto\\asn1\\i2d_pu.o",
                            "crypto\\asn1\\n_pkey.o",
                            "crypto\\asn1\\nsseq.o",
                            "crypto\\asn1\\p5_pbe.o",
                            "crypto\\asn1\\p5_pbev2.o",
                            "crypto\\asn1\\p5_scrypt.o",
                            "crypto\\asn1\\p8_pkey.o",
                            "crypto\\asn1\\t_bitst.o",
                            "crypto\\asn1\\t_pkey.o",
                            "crypto\\asn1\\t_spki.o",
                            "crypto\\asn1\\tasn_dec.o",
                            "crypto\\asn1\\tasn_enc.o",
                            "crypto\\asn1\\tasn_fre.o",
                            "crypto\\asn1\\tasn_new.o",
                            "crypto\\asn1\\tasn_prn.o",
                            "crypto\\asn1\\tasn_scn.o",
                            "crypto\\asn1\\tasn_typ.o",
                            "crypto\\asn1\\tasn_utl.o",
                            "crypto\\asn1\\x_algor.o",
                            "crypto\\asn1\\x_bignum.o",
                            "crypto\\asn1\\x_info.o",
                            "crypto\\asn1\\x_int64.o",
                            "crypto\\asn1\\x_long.o",
                            "crypto\\asn1\\x_pkey.o",
                            "crypto\\asn1\\x_sig.o",
                            "crypto\\asn1\\x_spki.o",
                            "crypto\\asn1\\x_val.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\async" =>
                {
                    "deps" =>
                        [
                            "crypto\\async\\async.o",
                            "crypto\\async\\async_err.o",
                            "crypto\\async\\async_wait.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\async\\arch" =>
                {
                    "deps" =>
                        [
                            "crypto\\async\\arch\\async_null.o",
                            "crypto\\async\\arch\\async_posix.o",
                            "crypto\\async\\arch\\async_win.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\bio" =>
                {
                    "deps" =>
                        [
                            "crypto\\bio\\b_addr.o",
                            "crypto\\bio\\b_dump.o",
                            "crypto\\bio\\b_print.o",
                            "crypto\\bio\\b_sock.o",
                            "crypto\\bio\\b_sock2.o",
                            "crypto\\bio\\bf_buff.o",
                            "crypto\\bio\\bf_lbuf.o",
                            "crypto\\bio\\bf_nbio.o",
                            "crypto\\bio\\bf_null.o",
                            "crypto\\bio\\bio_cb.o",
                            "crypto\\bio\\bio_err.o",
                            "crypto\\bio\\bio_lib.o",
                            "crypto\\bio\\bio_meth.o",
                            "crypto\\bio\\bss_acpt.o",
                            "crypto\\bio\\bss_bio.o",
                            "crypto\\bio\\bss_conn.o",
                            "crypto\\bio\\bss_dgram.o",
                            "crypto\\bio\\bss_fd.o",
                            "crypto\\bio\\bss_file.o",
                            "crypto\\bio\\bss_log.o",
                            "crypto\\bio\\bss_mem.o",
                            "crypto\\bio\\bss_null.o",
                            "crypto\\bio\\bss_sock.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\bn" =>
                {
                    "deps" =>
                        [
                            "crypto\\bn\\bn_add.o",
                            "crypto\\bn\\bn_asm.o",
                            "crypto\\bn\\bn_blind.o",
                            "crypto\\bn\\bn_const.o",
                            "crypto\\bn\\bn_ctx.o",
                            "crypto\\bn\\bn_depr.o",
                            "crypto\\bn\\bn_dh.o",
                            "crypto\\bn\\bn_div.o",
                            "crypto\\bn\\bn_err.o",
                            "crypto\\bn\\bn_exp.o",
                            "crypto\\bn\\bn_exp2.o",
                            "crypto\\bn\\bn_gcd.o",
                            "crypto\\bn\\bn_gf2m.o",
                            "crypto\\bn\\bn_intern.o",
                            "crypto\\bn\\bn_kron.o",
                            "crypto\\bn\\bn_lib.o",
                            "crypto\\bn\\bn_mod.o",
                            "crypto\\bn\\bn_mont.o",
                            "crypto\\bn\\bn_mpi.o",
                            "crypto\\bn\\bn_mul.o",
                            "crypto\\bn\\bn_nist.o",
                            "crypto\\bn\\bn_prime.o",
                            "crypto\\bn\\bn_print.o",
                            "crypto\\bn\\bn_rand.o",
                            "crypto\\bn\\bn_recp.o",
                            "crypto\\bn\\bn_shift.o",
                            "crypto\\bn\\bn_sqr.o",
                            "crypto\\bn\\bn_sqrt.o",
                            "crypto\\bn\\bn_srp.o",
                            "crypto\\bn\\bn_word.o",
                            "crypto\\bn\\bn_x931p.o",
                            "crypto\\bn\\rsaz-avx2.o",
                            "crypto\\bn\\rsaz-x86_64.o",
                            "crypto\\bn\\rsaz_exp.o",
                            "crypto\\bn\\x86_64-gf2m.o",
                            "crypto\\bn\\x86_64-mont.o",
                            "crypto\\bn\\x86_64-mont5.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\buffer" =>
                {
                    "deps" =>
                        [
                            "crypto\\buffer\\buf_err.o",
                            "crypto\\buffer\\buffer.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\chacha" =>
                {
                    "deps" =>
                        [
                            "crypto\\chacha\\chacha-x86_64.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\cmac" =>
                {
                    "deps" =>
                        [
                            "crypto\\cmac\\cm_ameth.o",
                            "crypto\\cmac\\cm_pmeth.o",
                            "crypto\\cmac\\cmac.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\conf" =>
                {
                    "deps" =>
                        [
                            "crypto\\conf\\conf_api.o",
                            "crypto\\conf\\conf_def.o",
                            "crypto\\conf\\conf_err.o",
                            "crypto\\conf\\conf_lib.o",
                            "crypto\\conf\\conf_mall.o",
                            "crypto\\conf\\conf_mod.o",
                            "crypto\\conf\\conf_sap.o",
                            "crypto\\conf\\conf_ssl.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\des" =>
                {
                    "deps" =>
                        [
                            "crypto\\des\\cbc_cksm.o",
                            "crypto\\des\\cbc_enc.o",
                            "crypto\\des\\cfb64ede.o",
                            "crypto\\des\\cfb64enc.o",
                            "crypto\\des\\cfb_enc.o",
                            "crypto\\des\\des_enc.o",
                            "crypto\\des\\ecb3_enc.o",
                            "crypto\\des\\ecb_enc.o",
                            "crypto\\des\\fcrypt.o",
                            "crypto\\des\\fcrypt_b.o",
                            "crypto\\des\\ofb64ede.o",
                            "crypto\\des\\ofb64enc.o",
                            "crypto\\des\\ofb_enc.o",
                            "crypto\\des\\pcbc_enc.o",
                            "crypto\\des\\qud_cksm.o",
                            "crypto\\des\\rand_key.o",
                            "crypto\\des\\set_key.o",
                            "crypto\\des\\str2key.o",
                            "crypto\\des\\xcbc_enc.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\dh" =>
                {
                    "deps" =>
                        [
                            "crypto\\dh\\dh_ameth.o",
                            "crypto\\dh\\dh_asn1.o",
                            "crypto\\dh\\dh_check.o",
                            "crypto\\dh\\dh_depr.o",
                            "crypto\\dh\\dh_err.o",
                            "crypto\\dh\\dh_gen.o",
                            "crypto\\dh\\dh_kdf.o",
                            "crypto\\dh\\dh_key.o",
                            "crypto\\dh\\dh_lib.o",
                            "crypto\\dh\\dh_meth.o",
                            "crypto\\dh\\dh_pmeth.o",
                            "crypto\\dh\\dh_prn.o",
                            "crypto\\dh\\dh_rfc5114.o",
                            "crypto\\dh\\dh_rfc7919.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\dsa" =>
                {
                    "deps" =>
                        [
                            "crypto\\dsa\\dsa_ameth.o",
                            "crypto\\dsa\\dsa_asn1.o",
                            "crypto\\dsa\\dsa_depr.o",
                            "crypto\\dsa\\dsa_err.o",
                            "crypto\\dsa\\dsa_gen.o",
                            "crypto\\dsa\\dsa_key.o",
                            "crypto\\dsa\\dsa_lib.o",
                            "crypto\\dsa\\dsa_meth.o",
                            "crypto\\dsa\\dsa_ossl.o",
                            "crypto\\dsa\\dsa_pmeth.o",
                            "crypto\\dsa\\dsa_prn.o",
                            "crypto\\dsa\\dsa_sign.o",
                            "crypto\\dsa\\dsa_vrf.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\dso" =>
                {
                    "deps" =>
                        [
                            "crypto\\dso\\dso_dl.o",
                            "crypto\\dso\\dso_dlfcn.o",
                            "crypto\\dso\\dso_err.o",
                            "crypto\\dso\\dso_lib.o",
                            "crypto\\dso\\dso_openssl.o",
                            "crypto\\dso\\dso_vms.o",
                            "crypto\\dso\\dso_win32.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\ec" =>
                {
                    "deps" =>
                        [
                            "crypto\\ec\\curve25519.o",
                            "crypto\\ec\\ec2_oct.o",
                            "crypto\\ec\\ec2_smpl.o",
                            "crypto\\ec\\ec_ameth.o",
                            "crypto\\ec\\ec_asn1.o",
                            "crypto\\ec\\ec_check.o",
                            "crypto\\ec\\ec_curve.o",
                            "crypto\\ec\\ec_cvt.o",
                            "crypto\\ec\\ec_err.o",
                            "crypto\\ec\\ec_key.o",
                            "crypto\\ec\\ec_kmeth.o",
                            "crypto\\ec\\ec_lib.o",
                            "crypto\\ec\\ec_mult.o",
                            "crypto\\ec\\ec_oct.o",
                            "crypto\\ec\\ec_pmeth.o",
                            "crypto\\ec\\ec_print.o",
                            "crypto\\ec\\ecdh_kdf.o",
                            "crypto\\ec\\ecdh_ossl.o",
                            "crypto\\ec\\ecdsa_ossl.o",
                            "crypto\\ec\\ecdsa_sign.o",
                            "crypto\\ec\\ecdsa_vrf.o",
                            "crypto\\ec\\eck_prn.o",
                            "crypto\\ec\\ecp_mont.o",
                            "crypto\\ec\\ecp_nist.o",
                            "crypto\\ec\\ecp_nistp224.o",
                            "crypto\\ec\\ecp_nistp256.o",
                            "crypto\\ec\\ecp_nistp521.o",
                            "crypto\\ec\\ecp_nistputil.o",
                            "crypto\\ec\\ecp_nistz256-x86_64.o",
                            "crypto\\ec\\ecp_nistz256.o",
                            "crypto\\ec\\ecp_oct.o",
                            "crypto\\ec\\ecp_smpl.o",
                            "crypto\\ec\\ecx_meth.o",
                            "crypto\\ec\\x25519-x86_64.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\ec\\curve448" =>
                {
                    "deps" =>
                        [
                            "crypto\\ec\\curve448\\curve448.o",
                            "crypto\\ec\\curve448\\curve448_tables.o",
                            "crypto\\ec\\curve448\\eddsa.o",
                            "crypto\\ec\\curve448\\f_generic.o",
                            "crypto\\ec\\curve448\\scalar.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\ec\\curve448\\arch_32" =>
                {
                    "deps" =>
                        [
                            "crypto\\ec\\curve448\\arch_32\\f_impl.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\engine" =>
                {
                    "deps" =>
                        [
                            "crypto\\engine\\eng_all.o",
                            "crypto\\engine\\eng_cnf.o",
                            "crypto\\engine\\eng_ctrl.o",
                            "crypto\\engine\\eng_dyn.o",
                            "crypto\\engine\\eng_err.o",
                            "crypto\\engine\\eng_fat.o",
                            "crypto\\engine\\eng_init.o",
                            "crypto\\engine\\eng_lib.o",
                            "crypto\\engine\\eng_list.o",
                            "crypto\\engine\\eng_openssl.o",
                            "crypto\\engine\\eng_pkey.o",
                            "crypto\\engine\\eng_rdrand.o",
                            "crypto\\engine\\eng_table.o",
                            "crypto\\engine\\tb_asnmth.o",
                            "crypto\\engine\\tb_cipher.o",
                            "crypto\\engine\\tb_dh.o",
                            "crypto\\engine\\tb_digest.o",
                            "crypto\\engine\\tb_dsa.o",
                            "crypto\\engine\\tb_eckey.o",
                            "crypto\\engine\\tb_pkmeth.o",
                            "crypto\\engine\\tb_rand.o",
                            "crypto\\engine\\tb_rsa.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\err" =>
                {
                    "deps" =>
                        [
                            "crypto\\err\\err.o",
                            "crypto\\err\\err_all.o",
                            "crypto\\err\\err_prn.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\evp" =>
                {
                    "deps" =>
                        [
                            "crypto\\evp\\bio_b64.o",
                            "crypto\\evp\\bio_enc.o",
                            "crypto\\evp\\bio_md.o",
                            "crypto\\evp\\bio_ok.o",
                            "crypto\\evp\\c_allc.o",
                            "crypto\\evp\\c_alld.o",
                            "crypto\\evp\\cmeth_lib.o",
                            "crypto\\evp\\digest.o",
                            "crypto\\evp\\e_aes.o",
                            "crypto\\evp\\e_aes_cbc_hmac_sha1.o",
                            "crypto\\evp\\e_aes_cbc_hmac_sha256.o",
                            "crypto\\evp\\e_aria.o",
                            "crypto\\evp\\e_bf.o",
                            "crypto\\evp\\e_camellia.o",
                            "crypto\\evp\\e_cast.o",
                            "crypto\\evp\\e_chacha20_poly1305.o",
                            "crypto\\evp\\e_des.o",
                            "crypto\\evp\\e_des3.o",
                            "crypto\\evp\\e_idea.o",
                            "crypto\\evp\\e_null.o",
                            "crypto\\evp\\e_old.o",
                            "crypto\\evp\\e_rc2.o",
                            "crypto\\evp\\e_rc4.o",
                            "crypto\\evp\\e_rc4_hmac_md5.o",
                            "crypto\\evp\\e_rc5.o",
                            "crypto\\evp\\e_seed.o",
                            "crypto\\evp\\e_sm4.o",
                            "crypto\\evp\\e_xcbc_d.o",
                            "crypto\\evp\\encode.o",
                            "crypto\\evp\\evp_cnf.o",
                            "crypto\\evp\\evp_enc.o",
                            "crypto\\evp\\evp_err.o",
                            "crypto\\evp\\evp_key.o",
                            "crypto\\evp\\evp_lib.o",
                            "crypto\\evp\\evp_pbe.o",
                            "crypto\\evp\\evp_pkey.o",
                            "crypto\\evp\\m_md2.o",
                            "crypto\\evp\\m_md4.o",
                            "crypto\\evp\\m_md5.o",
                            "crypto\\evp\\m_md5_sha1.o",
                            "crypto\\evp\\m_mdc2.o",
                            "crypto\\evp\\m_null.o",
                            "crypto\\evp\\m_ripemd.o",
                            "crypto\\evp\\m_sha1.o",
                            "crypto\\evp\\m_sha3.o",
                            "crypto\\evp\\m_sigver.o",
                            "crypto\\evp\\m_wp.o",
                            "crypto\\evp\\names.o",
                            "crypto\\evp\\p5_crpt.o",
                            "crypto\\evp\\p5_crpt2.o",
                            "crypto\\evp\\p_dec.o",
                            "crypto\\evp\\p_enc.o",
                            "crypto\\evp\\p_lib.o",
                            "crypto\\evp\\p_open.o",
                            "crypto\\evp\\p_seal.o",
                            "crypto\\evp\\p_sign.o",
                            "crypto\\evp\\p_verify.o",
                            "crypto\\evp\\pbe_scrypt.o",
                            "crypto\\evp\\pmeth_fn.o",
                            "crypto\\evp\\pmeth_gn.o",
                            "crypto\\evp\\pmeth_lib.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\hmac" =>
                {
                    "deps" =>
                        [
                            "crypto\\hmac\\hm_ameth.o",
                            "crypto\\hmac\\hm_pmeth.o",
                            "crypto\\hmac\\hmac.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\kdf" =>
                {
                    "deps" =>
                        [
                            "crypto\\kdf\\hkdf.o",
                            "crypto\\kdf\\kdf_err.o",
                            "crypto\\kdf\\scrypt.o",
                            "crypto\\kdf\\tls1_prf.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\lhash" =>
                {
                    "deps" =>
                        [
                            "crypto\\lhash\\lh_stats.o",
                            "crypto\\lhash\\lhash.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\md5" =>
                {
                    "deps" =>
                        [
                            "crypto\\md5\\md5-x86_64.o",
                            "crypto\\md5\\md5_dgst.o",
                            "crypto\\md5\\md5_one.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\modes" =>
                {
                    "deps" =>
                        [
                            "crypto\\modes\\aesni-gcm-x86_64.o",
                            "crypto\\modes\\cbc128.o",
                            "crypto\\modes\\ccm128.o",
                            "crypto\\modes\\cfb128.o",
                            "crypto\\modes\\ctr128.o",
                            "crypto\\modes\\cts128.o",
                            "crypto\\modes\\gcm128.o",
                            "crypto\\modes\\ghash-x86_64.o",
                            "crypto\\modes\\ocb128.o",
                            "crypto\\modes\\ofb128.o",
                            "crypto\\modes\\wrap128.o",
                            "crypto\\modes\\xts128.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\objects" =>
                {
                    "deps" =>
                        [
                            "crypto\\objects\\o_names.o",
                            "crypto\\objects\\obj_dat.o",
                            "crypto\\objects\\obj_err.o",
                            "crypto\\objects\\obj_lib.o",
                            "crypto\\objects\\obj_xref.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\ocsp" =>
                {
                    "deps" =>
                        [
                            "crypto\\ocsp\\ocsp_asn.o",
                            "crypto\\ocsp\\ocsp_cl.o",
                            "crypto\\ocsp\\ocsp_err.o",
                            "crypto\\ocsp\\ocsp_ext.o",
                            "crypto\\ocsp\\ocsp_ht.o",
                            "crypto\\ocsp\\ocsp_lib.o",
                            "crypto\\ocsp\\ocsp_prn.o",
                            "crypto\\ocsp\\ocsp_srv.o",
                            "crypto\\ocsp\\ocsp_vfy.o",
                            "crypto\\ocsp\\v3_ocsp.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\pem" =>
                {
                    "deps" =>
                        [
                            "crypto\\pem\\pem_all.o",
                            "crypto\\pem\\pem_err.o",
                            "crypto\\pem\\pem_info.o",
                            "crypto\\pem\\pem_lib.o",
                            "crypto\\pem\\pem_oth.o",
                            "crypto\\pem\\pem_pk8.o",
                            "crypto\\pem\\pem_pkey.o",
                            "crypto\\pem\\pem_sign.o",
                            "crypto\\pem\\pem_x509.o",
                            "crypto\\pem\\pem_xaux.o",
                            "crypto\\pem\\pvkfmt.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\pkcs12" =>
                {
                    "deps" =>
                        [
                            "crypto\\pkcs12\\p12_add.o",
                            "crypto\\pkcs12\\p12_asn.o",
                            "crypto\\pkcs12\\p12_attr.o",
                            "crypto\\pkcs12\\p12_crpt.o",
                            "crypto\\pkcs12\\p12_crt.o",
                            "crypto\\pkcs12\\p12_decr.o",
                            "crypto\\pkcs12\\p12_init.o",
                            "crypto\\pkcs12\\p12_key.o",
                            "crypto\\pkcs12\\p12_kiss.o",
                            "crypto\\pkcs12\\p12_mutl.o",
                            "crypto\\pkcs12\\p12_npas.o",
                            "crypto\\pkcs12\\p12_p8d.o",
                            "crypto\\pkcs12\\p12_p8e.o",
                            "crypto\\pkcs12\\p12_sbag.o",
                            "crypto\\pkcs12\\p12_utl.o",
                            "crypto\\pkcs12\\pk12err.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\pkcs7" =>
                {
                    "deps" =>
                        [
                            "crypto\\pkcs7\\bio_pk7.o",
                            "crypto\\pkcs7\\pk7_asn1.o",
                            "crypto\\pkcs7\\pk7_attr.o",
                            "crypto\\pkcs7\\pk7_doit.o",
                            "crypto\\pkcs7\\pk7_lib.o",
                            "crypto\\pkcs7\\pk7_mime.o",
                            "crypto\\pkcs7\\pk7_smime.o",
                            "crypto\\pkcs7\\pkcs7err.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\poly1305" =>
                {
                    "deps" =>
                        [
                            "crypto\\poly1305\\poly1305-x86_64.o",
                            "crypto\\poly1305\\poly1305.o",
                            "crypto\\poly1305\\poly1305_ameth.o",
                            "crypto\\poly1305\\poly1305_pmeth.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\rand" =>
                {
                    "deps" =>
                        [
                            "crypto\\rand\\drbg_ctr.o",
                            "crypto\\rand\\drbg_lib.o",
                            "crypto\\rand\\rand_egd.o",
                            "crypto\\rand\\rand_err.o",
                            "crypto\\rand\\rand_lib.o",
                            "crypto\\rand\\rand_unix.o",
                            "crypto\\rand\\rand_vms.o",
                            "crypto\\rand\\rand_win.o",
                            "crypto\\rand\\randfile.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\rsa" =>
                {
                    "deps" =>
                        [
                            "crypto\\rsa\\rsa_ameth.o",
                            "crypto\\rsa\\rsa_asn1.o",
                            "crypto\\rsa\\rsa_chk.o",
                            "crypto\\rsa\\rsa_crpt.o",
                            "crypto\\rsa\\rsa_depr.o",
                            "crypto\\rsa\\rsa_err.o",
                            "crypto\\rsa\\rsa_gen.o",
                            "crypto\\rsa\\rsa_lib.o",
                            "crypto\\rsa\\rsa_meth.o",
                            "crypto\\rsa\\rsa_mp.o",
                            "crypto\\rsa\\rsa_none.o",
                            "crypto\\rsa\\rsa_oaep.o",
                            "crypto\\rsa\\rsa_ossl.o",
                            "crypto\\rsa\\rsa_pk1.o",
                            "crypto\\rsa\\rsa_pmeth.o",
                            "crypto\\rsa\\rsa_prn.o",
                            "crypto\\rsa\\rsa_pss.o",
                            "crypto\\rsa\\rsa_saos.o",
                            "crypto\\rsa\\rsa_sign.o",
                            "crypto\\rsa\\rsa_ssl.o",
                            "crypto\\rsa\\rsa_x931.o",
                            "crypto\\rsa\\rsa_x931g.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\sha" =>
                {
                    "deps" =>
                        [
                            "crypto\\sha\\keccak1600-x86_64.o",
                            "crypto\\sha\\sha1-mb-x86_64.o",
                            "crypto\\sha\\sha1-x86_64.o",
                            "crypto\\sha\\sha1_one.o",
                            "crypto\\sha\\sha1dgst.o",
                            "crypto\\sha\\sha256-mb-x86_64.o",
                            "crypto\\sha\\sha256-x86_64.o",
                            "crypto\\sha\\sha256.o",
                            "crypto\\sha\\sha512-x86_64.o",
                            "crypto\\sha\\sha512.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\stack" =>
                {
                    "deps" =>
                        [
                            "crypto\\stack\\stack.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\store" =>
                {
                    "deps" =>
                        [
                            "crypto\\store\\loader_file.o",
                            "crypto\\store\\store_err.o",
                            "crypto\\store\\store_init.o",
                            "crypto\\store\\store_lib.o",
                            "crypto\\store\\store_register.o",
                            "crypto\\store\\store_strings.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\txt_db" =>
                {
                    "deps" =>
                        [
                            "crypto\\txt_db\\txt_db.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\ui" =>
                {
                    "deps" =>
                        [
                            "crypto\\ui\\ui_err.o",
                            "crypto\\ui\\ui_lib.o",
                            "crypto\\ui\\ui_null.o",
                            "crypto\\ui\\ui_openssl.o",
                            "crypto\\ui\\ui_util.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\x509" =>
                {
                    "deps" =>
                        [
                            "crypto\\x509\\by_dir.o",
                            "crypto\\x509\\by_file.o",
                            "crypto\\x509\\t_crl.o",
                            "crypto\\x509\\t_req.o",
                            "crypto\\x509\\t_x509.o",
                            "crypto\\x509\\x509_att.o",
                            "crypto\\x509\\x509_cmp.o",
                            "crypto\\x509\\x509_d2.o",
                            "crypto\\x509\\x509_def.o",
                            "crypto\\x509\\x509_err.o",
                            "crypto\\x509\\x509_ext.o",
                            "crypto\\x509\\x509_lu.o",
                            "crypto\\x509\\x509_meth.o",
                            "crypto\\x509\\x509_obj.o",
                            "crypto\\x509\\x509_r2x.o",
                            "crypto\\x509\\x509_req.o",
                            "crypto\\x509\\x509_set.o",
                            "crypto\\x509\\x509_trs.o",
                            "crypto\\x509\\x509_txt.o",
                            "crypto\\x509\\x509_v3.o",
                            "crypto\\x509\\x509_vfy.o",
                            "crypto\\x509\\x509_vpm.o",
                            "crypto\\x509\\x509cset.o",
                            "crypto\\x509\\x509name.o",
                            "crypto\\x509\\x509rset.o",
                            "crypto\\x509\\x509spki.o",
                            "crypto\\x509\\x509type.o",
                            "crypto\\x509\\x_all.o",
                            "crypto\\x509\\x_attrib.o",
                            "crypto\\x509\\x_crl.o",
                            "crypto\\x509\\x_exten.o",
                            "crypto\\x509\\x_name.o",
                            "crypto\\x509\\x_pubkey.o",
                            "crypto\\x509\\x_req.o",
                            "crypto\\x509\\x_x509.o",
                            "crypto\\x509\\x_x509a.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "crypto\\x509v3" =>
                {
                    "deps" =>
                        [
                            "crypto\\x509v3\\pcy_cache.o",
                            "crypto\\x509v3\\pcy_data.o",
                            "crypto\\x509v3\\pcy_lib.o",
                            "crypto\\x509v3\\pcy_map.o",
                            "crypto\\x509v3\\pcy_node.o",
                            "crypto\\x509v3\\pcy_tree.o",
                            "crypto\\x509v3\\v3_addr.o",
                            "crypto\\x509v3\\v3_admis.o",
                            "crypto\\x509v3\\v3_akey.o",
                            "crypto\\x509v3\\v3_akeya.o",
                            "crypto\\x509v3\\v3_alt.o",
                            "crypto\\x509v3\\v3_asid.o",
                            "crypto\\x509v3\\v3_bcons.o",
                            "crypto\\x509v3\\v3_bitst.o",
                            "crypto\\x509v3\\v3_conf.o",
                            "crypto\\x509v3\\v3_cpols.o",
                            "crypto\\x509v3\\v3_crld.o",
                            "crypto\\x509v3\\v3_enum.o",
                            "crypto\\x509v3\\v3_extku.o",
                            "crypto\\x509v3\\v3_genn.o",
                            "crypto\\x509v3\\v3_ia5.o",
                            "crypto\\x509v3\\v3_info.o",
                            "crypto\\x509v3\\v3_int.o",
                            "crypto\\x509v3\\v3_lib.o",
                            "crypto\\x509v3\\v3_ncons.o",
                            "crypto\\x509v3\\v3_pci.o",
                            "crypto\\x509v3\\v3_pcia.o",
                            "crypto\\x509v3\\v3_pcons.o",
                            "crypto\\x509v3\\v3_pku.o",
                            "crypto\\x509v3\\v3_pmaps.o",
                            "crypto\\x509v3\\v3_prn.o",
                            "crypto\\x509v3\\v3_purp.o",
                            "crypto\\x509v3\\v3_skey.o",
                            "crypto\\x509v3\\v3_sxnet.o",
                            "crypto\\x509v3\\v3_tlsf.o",
                            "crypto\\x509v3\\v3_utl.o",
                            "crypto\\x509v3\\v3err.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "engines" =>
                {
                    "deps" =>
                        [
                            "engines\\e_capi.o",
                            "engines\\e_padlock-x86_64.o",
                            "engines\\e_padlock.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libcrypto",
                                ],
                        },
                },
            "ssl" =>
                {
                    "deps" =>
                        [
                            "ssl\\bio_ssl.o",
                            "ssl\\d1_lib.o",
                            "ssl\\d1_msg.o",
                            "ssl\\d1_srtp.o",
                            "ssl\\methods.o",
                            "ssl\\packet.o",
                            "ssl\\pqueue.o",
                            "ssl\\s3_cbc.o",
                            "ssl\\s3_enc.o",
                            "ssl\\s3_lib.o",
                            "ssl\\s3_msg.o",
                            "ssl\\ssl_asn1.o",
                            "ssl\\ssl_cert.o",
                            "ssl\\ssl_ciph.o",
                            "ssl\\ssl_conf.o",
                            "ssl\\ssl_err.o",
                            "ssl\\ssl_init.o",
                            "ssl\\ssl_lib.o",
                            "ssl\\ssl_mcnf.o",
                            "ssl\\ssl_quic.o",
                            "ssl\\ssl_rsa.o",
                            "ssl\\ssl_sess.o",
                            "ssl\\ssl_stat.o",
                            "ssl\\ssl_txt.o",
                            "ssl\\ssl_utst.o",
                            "ssl\\t1_enc.o",
                            "ssl\\t1_lib.o",
                            "ssl\\t1_trce.o",
                            "ssl\\tls13_enc.o",
                            "ssl\\tls_srp.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libssl",
                                ],
                        },
                },
            "ssl\\record" =>
                {
                    "deps" =>
                        [
                            "ssl\\record\\dtls1_bitmap.o",
                            "ssl\\record\\rec_layer_d1.o",
                            "ssl\\record\\rec_layer_s3.o",
                            "ssl\\record\\ssl3_buffer.o",
                            "ssl\\record\\ssl3_record.o",
                            "ssl\\record\\ssl3_record_tls13.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libssl",
                                ],
                        },
                },
            "ssl\\statem" =>
                {
                    "deps" =>
                        [
                            "ssl\\statem\\extensions.o",
                            "ssl\\statem\\extensions_clnt.o",
                            "ssl\\statem\\extensions_cust.o",
                            "ssl\\statem\\extensions_srvr.o",
                            "ssl\\statem\\statem.o",
                            "ssl\\statem\\statem_clnt.o",
                            "ssl\\statem\\statem_dtls.o",
                            "ssl\\statem\\statem_lib.o",
                            "ssl\\statem\\statem_quic.o",
                            "ssl\\statem\\statem_srvr.o",
                        ],
                    "products" =>
                        {
                            "lib" =>
                                [
                                    "libssl",
                                ],
                        },
                },
        },
    "engines" =>
        [
        ],
    "extra" =>
        [
            "crypto\\alphacpuid.pl",
            "crypto\\arm64cpuid.pl",
            "crypto\\armv4cpuid.pl",
            "crypto\\ia64cpuid.S",
            "crypto\\pariscid.pl",
            "crypto\\ppccpuid.pl",
            "crypto\\x86_64cpuid.pl",
            "crypto\\x86cpuid.pl",
            "ms\\applink.c",
            "ms\\uplink-x86.pl",
            "ms\\uplink.c",
        ],
    "generate" =>
        {
            "apps\\openssl.rc" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkrc.pl",
                    "openssl",
                ],
            "apps\\progs.h" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\progs.pl",
                    "\$(APPS_OPENSSL)",
                ],
            "crypto\\aes\\aes-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\aes\\aes-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-ia64.S",
                ],
            "crypto\\aes\\aes-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-parisc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-parisc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aes-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aes-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesfx-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesfx-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesni-mb-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesni-mb-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesni-sha1-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesni-sha1-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesni-sha256-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesni-sha256-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesni-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesni-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\aes\\aesni-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesni-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesp8-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesp8-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aest4-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aest4-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\aesv8-armx.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\aesv8-armx.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\bsaes-armv7.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\bsaes-armv7.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\bsaes-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\bsaes-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\vpaes-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\vpaes-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\vpaes-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\vpaes-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\aes\\vpaes-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\vpaes-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\aes\\vpaes-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\asm\\vpaes-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\alphacpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\alphacpuid.pl",
                ],
            "crypto\\arm64cpuid.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\arm64cpuid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\armv4cpuid.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\armv4cpuid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\alpha-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\alpha-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\armv4-gf2m.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\armv4-gf2m.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\armv4-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\armv4-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\armv8-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\armv8-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\bn-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\bn-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\bn\\bn-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\ia64.S",
                ],
            "crypto\\bn\\bn-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\bn-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\co-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\co-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\bn\\ia64-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\ia64-mont.pl",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\bn\\mips-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\mips-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\parisc-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\parisc-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\ppc-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\ppc-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\ppc64-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\ppc64-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\rsaz-avx2.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\rsaz-avx2.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\rsaz-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\rsaz-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\s390x-gf2m.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\s390x-gf2m.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\s390x-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\s390x-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\sparct4-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\sparct4-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\sparcv9-gf2m.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\sparcv9-gf2m.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\sparcv9-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\sparcv9-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\sparcv9a-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\sparcv9a-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\vis3-mont.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\vis3-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\x86-gf2m.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\x86-gf2m.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\bn\\x86-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\x86-mont.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\bn\\x86_64-gf2m.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\x86_64-gf2m.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\x86_64-mont.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\x86_64-mont.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\bn\\x86_64-mont5.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\asm\\x86_64-mont5.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\buildinf.h" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkbuildinf.pl",
                    "\"\$(CC)",
                    "\$(LIB_CFLAGS)",
                    "\$(CPPFLAGS_Q)\"",
                    "\"\$(PLATFORM)\"",
                ],
            "crypto\\chacha\\chacha-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\chacha\\chacha-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\chacha\\chacha-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\chacha\\chacha-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\chacha\\chacha-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\chacha\\chacha-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\chacha\\asm\\chacha-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\des\\crypt586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\asm\\crypt586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\des\\des-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\asm\\des-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\des\\des_enc-sparc.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\asm\\des_enc.m4",
                ],
            "crypto\\des\\dest4-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\asm\\dest4-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-avx2.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-avx2.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-ppc64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-ppc64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\ecp_nistz256-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\ec\\ecp_nistz256-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\ecp_nistz256-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\x25519-ppc64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\x25519-ppc64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ec\\x25519-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\asm\\x25519-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ia64cpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ia64cpuid.S",
                ],
            "crypto\\md5\\md5-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\asm\\md5-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\md5\\md5-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\asm\\md5-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\md5\\md5-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\asm\\md5-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\aesni-gcm-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\aesni-gcm-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-alpha.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-alpha.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-ia64.pl",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\modes\\ghash-parisc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-parisc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghash-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\modes\\ghash-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghash-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghashp8-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghashp8-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\modes\\ghashv8-armx.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\asm\\ghashv8-armx.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\pariscid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pariscid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-ppcfp.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-ppcfp.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\poly1305\\poly1305-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\poly1305\\poly1305-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\asm\\poly1305-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\ppccpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ppccpuid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\s390xcpuid.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\s390xcpuid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\keccak1600-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\keccak1600-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\keccak1600-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\keccak1600-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\keccak1600-ppc64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\keccak1600-ppc64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\keccak1600-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\keccak1600-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\keccak1600-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\keccak1600-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\sha\\sha1-alpha.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-alpha.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-armv4-large.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-armv4-large.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-ia64.pl",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\sha\\sha1-mb-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-mb-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-parisc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-parisc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha1-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha1-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha256-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\sha\\sha256-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha256-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-ia64.pl",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\sha\\sha256-mb-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha256-mb-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-parisc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-parisc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha256p8-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512p8-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-586.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-586.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "crypto\\sha\\sha512-armv4.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-armv4.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-armv8.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-armv8.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-ia64.pl",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                ],
            "crypto\\sha\\sha512-mips.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-mips.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-parisc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-parisc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-s390x.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-s390x.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-sparcv9.S" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-sparcv9.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\sha\\sha512p8-ppc.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\asm\\sha512p8-ppc.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\uplink-ia64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ms\\uplink-ia64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\uplink-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ms\\uplink-x86.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\uplink-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ms\\uplink-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\x86_64cpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x86_64cpuid.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "crypto\\x86cpuid.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x86cpuid.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "engines\\e_padlock-x86.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\engines\\asm\\e_padlock-x86.pl",
                    "\$(PERLASM_SCHEME)",
                    "\$(LIB_CFLAGS)",
                    "\$(LIB_CPPFLAGS)",
                    "\$(PROCESSOR)",
                ],
            "engines\\e_padlock-x86_64.s" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\engines\\asm\\e_padlock-x86_64.pl",
                    "\$(PERLASM_SCHEME)",
                ],
            "include\\crypto\\bn_conf.h" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include\\crypto\\bn_conf.h.in",
                ],
            "include\\crypto\\dso_conf.h" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include\\crypto\\dso_conf.h.in",
                ],
            "include\\openssl\\opensslconf.h" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include\\openssl\\opensslconf.h.in",
                ],
            "libcrypto.def" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkdef.pl",
                    "crypto",
                    "32",
                ],
            "libcrypto.rc" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkrc.pl",
                    "libcrypto",
                ],
            "libssl.def" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkdef.pl",
                    "ssl",
                    "32",
                ],
            "libssl.rc" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\util\\mkrc.pl",
                    "libssl",
                ],
        },
    "includes" =>
        {
            "apps\\app_rand.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\apps.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\asn1pars.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\bf_prefix.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\ca.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\ciphers.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\cms.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\crl.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\crl2p7.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\dgst.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\dhparam.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\dsa.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\dsaparam.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\ec.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\ecparam.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\enc.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\engine.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\errstr.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\gendsa.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\genpkey.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\genrsa.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\nseq.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\ocsp.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\openssl.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\opt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\passwd.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkcs12.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkcs7.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkcs8.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkey.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkeyparam.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\pkeyutl.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\prime.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\progs.h" =>
                [
                    ".",
                ],
            "apps\\rand.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\rehash.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\req.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\rsa.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\rsautl.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\s_cb.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\s_client.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\s_server.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\s_socket.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\s_time.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\sess_id.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\smime.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\speed.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\spkac.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\storeutl.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\verify.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\version.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\win32_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "apps\\x509.o" =>
                [
                    ".",
                    "include",
                    "apps",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aes-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aes-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aes-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aes_cbc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_cfb.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_core.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_ecb.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_ige.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_misc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_ofb.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aes_wrap.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aesfx-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aesni-mb-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aesni-sha1-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aesni-sha256-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aesni-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\aes\\aest4-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\aesv8-armx.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\bsaes-armv7.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\aes\\vpaes-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\arm64cpuid.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\armv4cpuid.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\asn1\\a_bitstr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_d2i_fp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_digest.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_dup.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_gentm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_i2d_fp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_int.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_mbstr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_object.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_octet.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_print.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_strex.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_strnid.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_time.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_type.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_utctm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_utf8.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\a_verify.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\ameth_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn1_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn1_gen.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn1_item_list.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn1_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn1_par.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn_mime.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn_moid.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn_mstbl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\asn_pack.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\bio_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\bio_ndef.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\d2i_pr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\d2i_pu.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\evp_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\f_int.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\f_string.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\i2d_pr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\i2d_pu.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\n_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\nsseq.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\p5_pbe.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\p5_pbev2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\p5_scrypt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\p8_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\t_bitst.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\t_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\t_spki.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_dec.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_fre.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_new.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_scn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_typ.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\tasn_utl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_algor.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_bignum.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_info.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_int64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_long.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_sig.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_spki.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\asn1\\x_val.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\arch\\async_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\arch\\async_posix.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\arch\\async_win.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\async.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\async_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\async\\async_wait.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\b_addr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\b_dump.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\b_print.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\b_sock.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\b_sock2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bf_buff.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bf_lbuf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bf_nbio.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bf_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bio_cb.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bio_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bio_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bio_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_acpt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_bio.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_conn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_dgram.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_fd.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_file.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_log.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_mem.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bio\\bss_sock.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\armv4-gf2m.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\armv4-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\bn-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\bn_add.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_asm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_blind.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_const.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_ctx.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_depr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_dh.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_div.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_exp.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\bn_exp2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_gcd.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_gf2m.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_intern.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_kron.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_mod.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_mont.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_mpi.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_mul.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_nist.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_prime.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_print.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_rand.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_recp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_shift.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_sqr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_sqrt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_srp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_word.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\bn_x931p.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\mips-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\rsaz-avx2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\rsaz-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\rsaz_exp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\sparct4-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\sparcv9-gf2m.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\sparcv9-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\sparcv9a-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\vis3-mont.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\bn\\x86_64-gf2m.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\x86_64-mont.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\bn\\x86_64-mont5.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\buffer\\buf_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\buffer\\buffer.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\buildinf.h" =>
                [
                    ".",
                ],
            "crypto\\chacha\\chacha-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\chacha\\chacha-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\chacha\\chacha-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\chacha\\chacha-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cmac\\cm_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cmac\\cm_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cmac\\cmac.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_api.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_def.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_mall.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_mod.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_sap.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\conf\\conf_ssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cpt_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cryptlib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ctype.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\cversion.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\cbc_cksm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\cbc_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\cfb64ede.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\cfb64enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\cfb_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\des_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\dest4-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\des\\ecb3_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\ecb_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\fcrypt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\fcrypt_b.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\ofb64ede.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\ofb64enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\ofb_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\pcbc_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\qud_cksm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\rand_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\set_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\str2key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\des\\xcbc_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_check.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_depr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_gen.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_kdf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_rfc5114.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dh\\dh_rfc7919.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_depr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_gen.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_ossl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dsa\\dsa_vrf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_dl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_dlfcn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_openssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_vms.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\dso\\dso_win32.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ebcdic.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\curve25519.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\curve448\\arch_32\\f_impl.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\curve448\\curve448.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\curve448\\curve448_tables.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\curve448\\eddsa.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\curve448\\f_generic.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\curve448\\scalar.o" =>
                [
                    ".",
                    "include",
                    "crypto\\ec\\curve448\\arch_32",
                    "crypto\\ec\\curve448",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448",
                ],
            "crypto\\ec\\ec2_oct.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec2_smpl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_check.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_curve.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_cvt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_kmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_mult.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_oct.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ec_print.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecdh_kdf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecdh_ossl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecdsa_ossl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecdsa_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecdsa_vrf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\eck_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_mont.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nist.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistp224.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistp256.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistp521.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistputil.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistz256-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\ec\\ecp_nistz256-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\ec\\ecp_nistz256-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\ec\\ecp_nistz256-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_nistz256.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_oct.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecp_smpl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\ecx_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ec\\x25519-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_all.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_cnf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_ctrl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_dyn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_fat.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_list.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_openssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_rdrand.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\eng_table.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_asnmth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_cipher.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_dh.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_digest.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_dsa.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_eckey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_pkmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_rand.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\engine\\tb_rsa.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\err\\err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\err\\err_all.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\err\\err_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\bio_b64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\bio_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\bio_md.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\bio_ok.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\c_allc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\c_alld.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\cmeth_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\digest.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_aes.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_aes_cbc_hmac_sha1.o" =>
                [
                    ".",
                    "include",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_aes_cbc_hmac_sha256.o" =>
                [
                    ".",
                    "include",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_aria.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_bf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_camellia.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_cast.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_chacha20_poly1305.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_des.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\evp\\e_des3.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\evp\\e_idea.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_old.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_rc2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_rc4.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_rc4_hmac_md5.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_rc5.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_seed.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\e_sm4.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\evp\\e_xcbc_d.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\encode.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_cnf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_pbe.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\evp_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_md2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_md4.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_md5.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_md5_sha1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_mdc2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_ripemd.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_sha1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_sha3.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\evp\\m_sigver.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\m_wp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\names.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p5_crpt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p5_crpt2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_dec.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_open.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_seal.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\p_verify.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\pbe_scrypt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\pmeth_fn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\pmeth_gn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\evp\\pmeth_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ex_data.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\getenv.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\hmac\\hm_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\hmac\\hm_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\hmac\\hmac.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\kdf\\hkdf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\kdf\\kdf_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\kdf\\scrypt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\kdf\\tls1_prf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\lhash\\lh_stats.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\lhash\\lhash.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\md5\\md5-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\md5\\md5-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\md5\\md5_dgst.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\md5\\md5_one.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\mem.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\mem_dbg.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\mem_sec.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\aesni-gcm-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\cbc128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\ccm128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\cfb128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\ctr128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\cts128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\gcm128.o" =>
                [
                    ".",
                    "include",
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\modes\\ghash-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\modes\\ghash-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\modes\\ghash-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\modes\\ghash-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\ghashv8-armx.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\modes\\ocb128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\ofb128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\wrap128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\modes\\xts128.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_dir.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_fips.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_fopen.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_str.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\o_time.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\objects\\o_names.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\objects\\obj_dat.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\objects\\obj_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\objects\\obj_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\objects\\obj_xref.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_asn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_cl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_ext.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_ht.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_srv.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\ocsp_vfy.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ocsp\\v3_ocsp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_all.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_info.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_oth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_pk8.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_pkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_x509.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pem_xaux.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pem\\pvkfmt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_add.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_asn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_attr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_crpt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_crt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_decr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_key.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_kiss.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_mutl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_npas.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_p8d.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_p8e.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_sbag.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\p12_utl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs12\\pk12err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\bio_pk7.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_attr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_doit.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_mime.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pk7_smime.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\pkcs7\\pkcs7err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\poly1305\\poly1305-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\poly1305\\poly1305-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\poly1305\\poly1305-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\poly1305\\poly1305-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\poly1305\\poly1305-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\poly1305\\poly1305.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\poly1305\\poly1305_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\poly1305\\poly1305_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\drbg_ctr.o" =>
                [
                    ".",
                    "include",
                    "crypto\\modes",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes",
                ],
            "crypto\\rand\\drbg_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_egd.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_unix.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_vms.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\rand_win.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rand\\randfile.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_ameth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_chk.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_crpt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_depr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_gen.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_mp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_none.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_oaep.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_ossl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_pk1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_pmeth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_pss.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_saos.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_sign.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_ssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_x931.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\rsa\\rsa_x931g.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\s390xcpuid.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\keccak1600-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\keccak1600-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha1-armv4-large.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha1-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha1-mb-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha1-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha1-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha1-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha1-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha1_one.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha1dgst.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha256-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha256-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha256-mb-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha256-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha256-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha256-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha256-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha256.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha512-armv4.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha512-armv8.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha512-mips.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha512-s390x.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha512-sparcv9.o" =>
                [
                    "crypto",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto",
                ],
            "crypto\\sha\\sha512-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\sha\\sha512.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\stack\\stack.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\loader_file.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\store_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\store_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\store_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\store_register.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\store\\store_strings.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\threads_none.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\threads_pthread.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\threads_win.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\txt_db\\txt_db.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ui\\ui_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ui\\ui_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ui\\ui_null.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ui\\ui_openssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\ui\\ui_util.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\uid.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\by_dir.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\by_file.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\t_crl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\t_req.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\t_x509.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_att.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_cmp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_d2.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_def.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_ext.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_lu.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_meth.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_obj.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_r2x.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_req.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_set.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_trs.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_txt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_v3.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_vfy.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509_vpm.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509cset.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509name.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509rset.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509spki.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x509type.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_all.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_attrib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_crl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_exten.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_name.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_pubkey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_req.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_x509.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509\\x_x509a.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_cache.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_data.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_map.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_node.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\pcy_tree.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_addr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_admis.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_akey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_akeya.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_alt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_asid.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_bcons.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_bitst.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_conf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_cpols.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_crld.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_enum.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_extku.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_genn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_ia5.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_info.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_int.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_ncons.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_pci.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_pcia.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_pcons.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_pku.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_pmaps.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_prn.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_purp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_skey.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_sxnet.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_tlsf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3_utl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x509v3\\v3err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "crypto\\x86_64cpuid.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "engines\\e_capi.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "engines\\e_padlock-x86_64.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "engines\\e_padlock.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "include\\crypto\\bn_conf.h" =>
                [
                    ".",
                ],
            "include\\crypto\\dso_conf.h" =>
                [
                    ".",
                ],
            "include\\openssl\\opensslconf.h" =>
                [
                    ".",
                ],
            "ssl\\bio_ssl.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\d1_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\d1_msg.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\d1_srtp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\methods.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\packet.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\pqueue.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\dtls1_bitmap.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\rec_layer_d1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\rec_layer_s3.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\ssl3_buffer.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\ssl3_record.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\record\\ssl3_record_tls13.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\s3_cbc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\s3_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\s3_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\s3_msg.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_asn1.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_cert.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_ciph.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_conf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_err.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_init.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_mcnf.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_quic.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_rsa.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_sess.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_stat.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_txt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\ssl_utst.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\extensions.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\extensions_clnt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\extensions_cust.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\extensions_srvr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem_clnt.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem_dtls.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem_quic.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\statem\\statem_srvr.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\t1_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\t1_lib.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\t1_trce.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\tls13_enc.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
            "ssl\\tls_srp.o" =>
                [
                    ".",
                    "include",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl",
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\include",
                ],
        },
    "install" =>
        {
            "libraries" =>
                [
                    "libcrypto",
                    "libssl",
                ],
            "programs" =>
                [
                    "apps\\openssl",
                ],
            "scripts" =>
                [
                    "apps\\CA.pl",
                    "apps\\tsget.pl",
                    "tools\\c_rehash.pl",
                ],
        },
    "ldadd" =>
        {
        },
    "libraries" =>
        [
            "apps\\libapps.a",
            "libcrypto",
            "libssl",
        ],
    "overrides" =>
        [
        ],
    "programs" =>
        [
            "apps\\openssl",
        ],
    "rawlines" =>
        [
        ],
    "rename" =>
        {
        },
    "scripts" =>
        [
            "apps\\CA.pl",
            "apps\\tsget.pl",
            "tools\\c_rehash.pl",
        ],
    "shared_sources" =>
        {
        },
    "sources" =>
        {
            "apps\\CA.pl" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\CA.pl.in",
                ],
            "apps\\app_rand.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\app_rand.c",
                ],
            "apps\\apps.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\apps.c",
                ],
            "apps\\asn1pars.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\asn1pars.c",
                ],
            "apps\\bf_prefix.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\bf_prefix.c",
                ],
            "apps\\ca.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\ca.c",
                ],
            "apps\\ciphers.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\ciphers.c",
                ],
            "apps\\cms.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\cms.c",
                ],
            "apps\\crl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\crl.c",
                ],
            "apps\\crl2p7.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\crl2p7.c",
                ],
            "apps\\dgst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\dgst.c",
                ],
            "apps\\dhparam.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\dhparam.c",
                ],
            "apps\\dsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\dsa.c",
                ],
            "apps\\dsaparam.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\dsaparam.c",
                ],
            "apps\\ec.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\ec.c",
                ],
            "apps\\ecparam.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\ecparam.c",
                ],
            "apps\\enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\enc.c",
                ],
            "apps\\engine.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\engine.c",
                ],
            "apps\\errstr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\errstr.c",
                ],
            "apps\\gendsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\gendsa.c",
                ],
            "apps\\genpkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\genpkey.c",
                ],
            "apps\\genrsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\genrsa.c",
                ],
            "apps\\libapps.a" =>
                [
                    "apps\\app_rand.o",
                    "apps\\apps.o",
                    "apps\\bf_prefix.o",
                    "apps\\opt.o",
                    "apps\\s_cb.o",
                    "apps\\s_socket.o",
                    "apps\\win32_init.o",
                ],
            "apps\\nseq.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\nseq.c",
                ],
            "apps\\ocsp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\ocsp.c",
                ],
            "apps\\openssl" =>
                [
                    "apps\\asn1pars.o",
                    "apps\\ca.o",
                    "apps\\ciphers.o",
                    "apps\\cms.o",
                    "apps\\crl.o",
                    "apps\\crl2p7.o",
                    "apps\\dgst.o",
                    "apps\\dhparam.o",
                    "apps\\dsa.o",
                    "apps\\dsaparam.o",
                    "apps\\ec.o",
                    "apps\\ecparam.o",
                    "apps\\enc.o",
                    "apps\\engine.o",
                    "apps\\errstr.o",
                    "apps\\gendsa.o",
                    "apps\\genpkey.o",
                    "apps\\genrsa.o",
                    "apps\\nseq.o",
                    "apps\\ocsp.o",
                    "apps\\openssl.o",
                    "apps\\openssl.res",
                    "apps\\passwd.o",
                    "apps\\pkcs12.o",
                    "apps\\pkcs7.o",
                    "apps\\pkcs8.o",
                    "apps\\pkey.o",
                    "apps\\pkeyparam.o",
                    "apps\\pkeyutl.o",
                    "apps\\prime.o",
                    "apps\\rand.o",
                    "apps\\rehash.o",
                    "apps\\req.o",
                    "apps\\rsa.o",
                    "apps\\rsautl.o",
                    "apps\\s_client.o",
                    "apps\\s_server.o",
                    "apps\\s_time.o",
                    "apps\\sess_id.o",
                    "apps\\smime.o",
                    "apps\\speed.o",
                    "apps\\spkac.o",
                    "apps\\storeutl.o",
                    "apps\\verify.o",
                    "apps\\version.o",
                    "apps\\x509.o",
                ],
            "apps\\openssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\openssl.c",
                ],
            "apps\\openssl.res" =>
                [
                    "apps\\openssl.rc",
                ],
            "apps\\opt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\opt.c",
                ],
            "apps\\passwd.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\passwd.c",
                ],
            "apps\\pkcs12.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkcs12.c",
                ],
            "apps\\pkcs7.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkcs7.c",
                ],
            "apps\\pkcs8.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkcs8.c",
                ],
            "apps\\pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkey.c",
                ],
            "apps\\pkeyparam.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkeyparam.c",
                ],
            "apps\\pkeyutl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\pkeyutl.c",
                ],
            "apps\\prime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\prime.c",
                ],
            "apps\\rand.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\rand.c",
                ],
            "apps\\rehash.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\rehash.c",
                ],
            "apps\\req.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\req.c",
                ],
            "apps\\rsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\rsa.c",
                ],
            "apps\\rsautl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\rsautl.c",
                ],
            "apps\\s_cb.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\s_cb.c",
                ],
            "apps\\s_client.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\s_client.c",
                ],
            "apps\\s_server.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\s_server.c",
                ],
            "apps\\s_socket.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\s_socket.c",
                ],
            "apps\\s_time.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\s_time.c",
                ],
            "apps\\sess_id.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\sess_id.c",
                ],
            "apps\\smime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\smime.c",
                ],
            "apps\\speed.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\speed.c",
                ],
            "apps\\spkac.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\spkac.c",
                ],
            "apps\\storeutl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\storeutl.c",
                ],
            "apps\\tsget.pl" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\tsget.in",
                ],
            "apps\\verify.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\verify.c",
                ],
            "apps\\version.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\version.c",
                ],
            "apps\\win32_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\win32_init.c",
                ],
            "apps\\x509.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\apps\\x509.c",
                ],
            "crypto\\aes\\aes_cbc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_cbc.c",
                ],
            "crypto\\aes\\aes_cfb.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_cfb.c",
                ],
            "crypto\\aes\\aes_core.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_core.c",
                ],
            "crypto\\aes\\aes_ecb.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_ecb.c",
                ],
            "crypto\\aes\\aes_ige.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_ige.c",
                ],
            "crypto\\aes\\aes_misc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_misc.c",
                ],
            "crypto\\aes\\aes_ofb.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_ofb.c",
                ],
            "crypto\\aes\\aes_wrap.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\aes\\aes_wrap.c",
                ],
            "crypto\\aes\\aesni-mb-x86_64.o" =>
                [
                    "crypto\\aes\\aesni-mb-x86_64.s",
                ],
            "crypto\\aes\\aesni-sha1-x86_64.o" =>
                [
                    "crypto\\aes\\aesni-sha1-x86_64.s",
                ],
            "crypto\\aes\\aesni-sha256-x86_64.o" =>
                [
                    "crypto\\aes\\aesni-sha256-x86_64.s",
                ],
            "crypto\\aes\\aesni-x86_64.o" =>
                [
                    "crypto\\aes\\aesni-x86_64.s",
                ],
            "crypto\\aes\\vpaes-x86_64.o" =>
                [
                    "crypto\\aes\\vpaes-x86_64.s",
                ],
            "crypto\\asn1\\a_bitstr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_bitstr.c",
                ],
            "crypto\\asn1\\a_d2i_fp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_d2i_fp.c",
                ],
            "crypto\\asn1\\a_digest.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_digest.c",
                ],
            "crypto\\asn1\\a_dup.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_dup.c",
                ],
            "crypto\\asn1\\a_gentm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_gentm.c",
                ],
            "crypto\\asn1\\a_i2d_fp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_i2d_fp.c",
                ],
            "crypto\\asn1\\a_int.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_int.c",
                ],
            "crypto\\asn1\\a_mbstr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_mbstr.c",
                ],
            "crypto\\asn1\\a_object.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_object.c",
                ],
            "crypto\\asn1\\a_octet.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_octet.c",
                ],
            "crypto\\asn1\\a_print.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_print.c",
                ],
            "crypto\\asn1\\a_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_sign.c",
                ],
            "crypto\\asn1\\a_strex.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_strex.c",
                ],
            "crypto\\asn1\\a_strnid.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_strnid.c",
                ],
            "crypto\\asn1\\a_time.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_time.c",
                ],
            "crypto\\asn1\\a_type.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_type.c",
                ],
            "crypto\\asn1\\a_utctm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_utctm.c",
                ],
            "crypto\\asn1\\a_utf8.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_utf8.c",
                ],
            "crypto\\asn1\\a_verify.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\a_verify.c",
                ],
            "crypto\\asn1\\ameth_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\ameth_lib.c",
                ],
            "crypto\\asn1\\asn1_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn1_err.c",
                ],
            "crypto\\asn1\\asn1_gen.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn1_gen.c",
                ],
            "crypto\\asn1\\asn1_item_list.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn1_item_list.c",
                ],
            "crypto\\asn1\\asn1_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn1_lib.c",
                ],
            "crypto\\asn1\\asn1_par.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn1_par.c",
                ],
            "crypto\\asn1\\asn_mime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn_mime.c",
                ],
            "crypto\\asn1\\asn_moid.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn_moid.c",
                ],
            "crypto\\asn1\\asn_mstbl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn_mstbl.c",
                ],
            "crypto\\asn1\\asn_pack.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\asn_pack.c",
                ],
            "crypto\\asn1\\bio_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\bio_asn1.c",
                ],
            "crypto\\asn1\\bio_ndef.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\bio_ndef.c",
                ],
            "crypto\\asn1\\d2i_pr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\d2i_pr.c",
                ],
            "crypto\\asn1\\d2i_pu.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\d2i_pu.c",
                ],
            "crypto\\asn1\\evp_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\evp_asn1.c",
                ],
            "crypto\\asn1\\f_int.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\f_int.c",
                ],
            "crypto\\asn1\\f_string.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\f_string.c",
                ],
            "crypto\\asn1\\i2d_pr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\i2d_pr.c",
                ],
            "crypto\\asn1\\i2d_pu.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\i2d_pu.c",
                ],
            "crypto\\asn1\\n_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\n_pkey.c",
                ],
            "crypto\\asn1\\nsseq.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\nsseq.c",
                ],
            "crypto\\asn1\\p5_pbe.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\p5_pbe.c",
                ],
            "crypto\\asn1\\p5_pbev2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\p5_pbev2.c",
                ],
            "crypto\\asn1\\p5_scrypt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\p5_scrypt.c",
                ],
            "crypto\\asn1\\p8_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\p8_pkey.c",
                ],
            "crypto\\asn1\\t_bitst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\t_bitst.c",
                ],
            "crypto\\asn1\\t_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\t_pkey.c",
                ],
            "crypto\\asn1\\t_spki.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\t_spki.c",
                ],
            "crypto\\asn1\\tasn_dec.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_dec.c",
                ],
            "crypto\\asn1\\tasn_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_enc.c",
                ],
            "crypto\\asn1\\tasn_fre.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_fre.c",
                ],
            "crypto\\asn1\\tasn_new.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_new.c",
                ],
            "crypto\\asn1\\tasn_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_prn.c",
                ],
            "crypto\\asn1\\tasn_scn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_scn.c",
                ],
            "crypto\\asn1\\tasn_typ.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_typ.c",
                ],
            "crypto\\asn1\\tasn_utl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\tasn_utl.c",
                ],
            "crypto\\asn1\\x_algor.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_algor.c",
                ],
            "crypto\\asn1\\x_bignum.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_bignum.c",
                ],
            "crypto\\asn1\\x_info.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_info.c",
                ],
            "crypto\\asn1\\x_int64.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_int64.c",
                ],
            "crypto\\asn1\\x_long.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_long.c",
                ],
            "crypto\\asn1\\x_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_pkey.c",
                ],
            "crypto\\asn1\\x_sig.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_sig.c",
                ],
            "crypto\\asn1\\x_spki.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_spki.c",
                ],
            "crypto\\asn1\\x_val.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\asn1\\x_val.c",
                ],
            "crypto\\async\\arch\\async_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\arch\\async_null.c",
                ],
            "crypto\\async\\arch\\async_posix.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\arch\\async_posix.c",
                ],
            "crypto\\async\\arch\\async_win.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\arch\\async_win.c",
                ],
            "crypto\\async\\async.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\async.c",
                ],
            "crypto\\async\\async_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\async_err.c",
                ],
            "crypto\\async\\async_wait.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\async\\async_wait.c",
                ],
            "crypto\\bio\\b_addr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\b_addr.c",
                ],
            "crypto\\bio\\b_dump.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\b_dump.c",
                ],
            "crypto\\bio\\b_print.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\b_print.c",
                ],
            "crypto\\bio\\b_sock.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\b_sock.c",
                ],
            "crypto\\bio\\b_sock2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\b_sock2.c",
                ],
            "crypto\\bio\\bf_buff.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bf_buff.c",
                ],
            "crypto\\bio\\bf_lbuf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bf_lbuf.c",
                ],
            "crypto\\bio\\bf_nbio.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bf_nbio.c",
                ],
            "crypto\\bio\\bf_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bf_null.c",
                ],
            "crypto\\bio\\bio_cb.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bio_cb.c",
                ],
            "crypto\\bio\\bio_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bio_err.c",
                ],
            "crypto\\bio\\bio_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bio_lib.c",
                ],
            "crypto\\bio\\bio_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bio_meth.c",
                ],
            "crypto\\bio\\bss_acpt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_acpt.c",
                ],
            "crypto\\bio\\bss_bio.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_bio.c",
                ],
            "crypto\\bio\\bss_conn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_conn.c",
                ],
            "crypto\\bio\\bss_dgram.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_dgram.c",
                ],
            "crypto\\bio\\bss_fd.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_fd.c",
                ],
            "crypto\\bio\\bss_file.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_file.c",
                ],
            "crypto\\bio\\bss_log.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_log.c",
                ],
            "crypto\\bio\\bss_mem.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_mem.c",
                ],
            "crypto\\bio\\bss_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_null.c",
                ],
            "crypto\\bio\\bss_sock.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bio\\bss_sock.c",
                ],
            "crypto\\bn\\bn_add.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_add.c",
                ],
            "crypto\\bn\\bn_asm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_asm.c",
                ],
            "crypto\\bn\\bn_blind.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_blind.c",
                ],
            "crypto\\bn\\bn_const.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_const.c",
                ],
            "crypto\\bn\\bn_ctx.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_ctx.c",
                ],
            "crypto\\bn\\bn_depr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_depr.c",
                ],
            "crypto\\bn\\bn_dh.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_dh.c",
                ],
            "crypto\\bn\\bn_div.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_div.c",
                ],
            "crypto\\bn\\bn_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_err.c",
                ],
            "crypto\\bn\\bn_exp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_exp.c",
                ],
            "crypto\\bn\\bn_exp2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_exp2.c",
                ],
            "crypto\\bn\\bn_gcd.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_gcd.c",
                ],
            "crypto\\bn\\bn_gf2m.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_gf2m.c",
                ],
            "crypto\\bn\\bn_intern.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_intern.c",
                ],
            "crypto\\bn\\bn_kron.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_kron.c",
                ],
            "crypto\\bn\\bn_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_lib.c",
                ],
            "crypto\\bn\\bn_mod.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_mod.c",
                ],
            "crypto\\bn\\bn_mont.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_mont.c",
                ],
            "crypto\\bn\\bn_mpi.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_mpi.c",
                ],
            "crypto\\bn\\bn_mul.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_mul.c",
                ],
            "crypto\\bn\\bn_nist.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_nist.c",
                ],
            "crypto\\bn\\bn_prime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_prime.c",
                ],
            "crypto\\bn\\bn_print.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_print.c",
                ],
            "crypto\\bn\\bn_rand.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_rand.c",
                ],
            "crypto\\bn\\bn_recp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_recp.c",
                ],
            "crypto\\bn\\bn_shift.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_shift.c",
                ],
            "crypto\\bn\\bn_sqr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_sqr.c",
                ],
            "crypto\\bn\\bn_sqrt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_sqrt.c",
                ],
            "crypto\\bn\\bn_srp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_srp.c",
                ],
            "crypto\\bn\\bn_word.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_word.c",
                ],
            "crypto\\bn\\bn_x931p.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\bn_x931p.c",
                ],
            "crypto\\bn\\rsaz-avx2.o" =>
                [
                    "crypto\\bn\\rsaz-avx2.s",
                ],
            "crypto\\bn\\rsaz-x86_64.o" =>
                [
                    "crypto\\bn\\rsaz-x86_64.s",
                ],
            "crypto\\bn\\rsaz_exp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\bn\\rsaz_exp.c",
                ],
            "crypto\\bn\\x86_64-gf2m.o" =>
                [
                    "crypto\\bn\\x86_64-gf2m.s",
                ],
            "crypto\\bn\\x86_64-mont.o" =>
                [
                    "crypto\\bn\\x86_64-mont.s",
                ],
            "crypto\\bn\\x86_64-mont5.o" =>
                [
                    "crypto\\bn\\x86_64-mont5.s",
                ],
            "crypto\\buffer\\buf_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\buffer\\buf_err.c",
                ],
            "crypto\\buffer\\buffer.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\buffer\\buffer.c",
                ],
            "crypto\\chacha\\chacha-x86_64.o" =>
                [
                    "crypto\\chacha\\chacha-x86_64.s",
                ],
            "crypto\\cmac\\cm_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cmac\\cm_ameth.c",
                ],
            "crypto\\cmac\\cm_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cmac\\cm_pmeth.c",
                ],
            "crypto\\cmac\\cmac.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cmac\\cmac.c",
                ],
            "crypto\\conf\\conf_api.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_api.c",
                ],
            "crypto\\conf\\conf_def.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_def.c",
                ],
            "crypto\\conf\\conf_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_err.c",
                ],
            "crypto\\conf\\conf_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_lib.c",
                ],
            "crypto\\conf\\conf_mall.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_mall.c",
                ],
            "crypto\\conf\\conf_mod.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_mod.c",
                ],
            "crypto\\conf\\conf_sap.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_sap.c",
                ],
            "crypto\\conf\\conf_ssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\conf\\conf_ssl.c",
                ],
            "crypto\\cpt_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cpt_err.c",
                ],
            "crypto\\cryptlib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cryptlib.c",
                ],
            "crypto\\ctype.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ctype.c",
                ],
            "crypto\\cversion.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\cversion.c",
                ],
            "crypto\\des\\cbc_cksm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\cbc_cksm.c",
                ],
            "crypto\\des\\cbc_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\cbc_enc.c",
                ],
            "crypto\\des\\cfb64ede.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\cfb64ede.c",
                ],
            "crypto\\des\\cfb64enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\cfb64enc.c",
                ],
            "crypto\\des\\cfb_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\cfb_enc.c",
                ],
            "crypto\\des\\des_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\des_enc.c",
                ],
            "crypto\\des\\ecb3_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\ecb3_enc.c",
                ],
            "crypto\\des\\ecb_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\ecb_enc.c",
                ],
            "crypto\\des\\fcrypt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\fcrypt.c",
                ],
            "crypto\\des\\fcrypt_b.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\fcrypt_b.c",
                ],
            "crypto\\des\\ofb64ede.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\ofb64ede.c",
                ],
            "crypto\\des\\ofb64enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\ofb64enc.c",
                ],
            "crypto\\des\\ofb_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\ofb_enc.c",
                ],
            "crypto\\des\\pcbc_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\pcbc_enc.c",
                ],
            "crypto\\des\\qud_cksm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\qud_cksm.c",
                ],
            "crypto\\des\\rand_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\rand_key.c",
                ],
            "crypto\\des\\set_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\set_key.c",
                ],
            "crypto\\des\\str2key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\str2key.c",
                ],
            "crypto\\des\\xcbc_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\des\\xcbc_enc.c",
                ],
            "crypto\\dh\\dh_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_ameth.c",
                ],
            "crypto\\dh\\dh_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_asn1.c",
                ],
            "crypto\\dh\\dh_check.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_check.c",
                ],
            "crypto\\dh\\dh_depr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_depr.c",
                ],
            "crypto\\dh\\dh_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_err.c",
                ],
            "crypto\\dh\\dh_gen.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_gen.c",
                ],
            "crypto\\dh\\dh_kdf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_kdf.c",
                ],
            "crypto\\dh\\dh_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_key.c",
                ],
            "crypto\\dh\\dh_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_lib.c",
                ],
            "crypto\\dh\\dh_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_meth.c",
                ],
            "crypto\\dh\\dh_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_pmeth.c",
                ],
            "crypto\\dh\\dh_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_prn.c",
                ],
            "crypto\\dh\\dh_rfc5114.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_rfc5114.c",
                ],
            "crypto\\dh\\dh_rfc7919.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dh\\dh_rfc7919.c",
                ],
            "crypto\\dsa\\dsa_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_ameth.c",
                ],
            "crypto\\dsa\\dsa_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_asn1.c",
                ],
            "crypto\\dsa\\dsa_depr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_depr.c",
                ],
            "crypto\\dsa\\dsa_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_err.c",
                ],
            "crypto\\dsa\\dsa_gen.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_gen.c",
                ],
            "crypto\\dsa\\dsa_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_key.c",
                ],
            "crypto\\dsa\\dsa_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_lib.c",
                ],
            "crypto\\dsa\\dsa_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_meth.c",
                ],
            "crypto\\dsa\\dsa_ossl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_ossl.c",
                ],
            "crypto\\dsa\\dsa_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_pmeth.c",
                ],
            "crypto\\dsa\\dsa_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_prn.c",
                ],
            "crypto\\dsa\\dsa_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_sign.c",
                ],
            "crypto\\dsa\\dsa_vrf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dsa\\dsa_vrf.c",
                ],
            "crypto\\dso\\dso_dl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_dl.c",
                ],
            "crypto\\dso\\dso_dlfcn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_dlfcn.c",
                ],
            "crypto\\dso\\dso_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_err.c",
                ],
            "crypto\\dso\\dso_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_lib.c",
                ],
            "crypto\\dso\\dso_openssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_openssl.c",
                ],
            "crypto\\dso\\dso_vms.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_vms.c",
                ],
            "crypto\\dso\\dso_win32.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\dso\\dso_win32.c",
                ],
            "crypto\\ebcdic.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ebcdic.c",
                ],
            "crypto\\ec\\curve25519.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve25519.c",
                ],
            "crypto\\ec\\curve448\\arch_32\\f_impl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\arch_32\\f_impl.c",
                ],
            "crypto\\ec\\curve448\\curve448.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\curve448.c",
                ],
            "crypto\\ec\\curve448\\curve448_tables.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\curve448_tables.c",
                ],
            "crypto\\ec\\curve448\\eddsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\eddsa.c",
                ],
            "crypto\\ec\\curve448\\f_generic.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\f_generic.c",
                ],
            "crypto\\ec\\curve448\\scalar.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\curve448\\scalar.c",
                ],
            "crypto\\ec\\ec2_oct.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec2_oct.c",
                ],
            "crypto\\ec\\ec2_smpl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec2_smpl.c",
                ],
            "crypto\\ec\\ec_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_ameth.c",
                ],
            "crypto\\ec\\ec_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_asn1.c",
                ],
            "crypto\\ec\\ec_check.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_check.c",
                ],
            "crypto\\ec\\ec_curve.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_curve.c",
                ],
            "crypto\\ec\\ec_cvt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_cvt.c",
                ],
            "crypto\\ec\\ec_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_err.c",
                ],
            "crypto\\ec\\ec_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_key.c",
                ],
            "crypto\\ec\\ec_kmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_kmeth.c",
                ],
            "crypto\\ec\\ec_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_lib.c",
                ],
            "crypto\\ec\\ec_mult.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_mult.c",
                ],
            "crypto\\ec\\ec_oct.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_oct.c",
                ],
            "crypto\\ec\\ec_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_pmeth.c",
                ],
            "crypto\\ec\\ec_print.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ec_print.c",
                ],
            "crypto\\ec\\ecdh_kdf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecdh_kdf.c",
                ],
            "crypto\\ec\\ecdh_ossl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecdh_ossl.c",
                ],
            "crypto\\ec\\ecdsa_ossl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecdsa_ossl.c",
                ],
            "crypto\\ec\\ecdsa_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecdsa_sign.c",
                ],
            "crypto\\ec\\ecdsa_vrf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecdsa_vrf.c",
                ],
            "crypto\\ec\\eck_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\eck_prn.c",
                ],
            "crypto\\ec\\ecp_mont.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_mont.c",
                ],
            "crypto\\ec\\ecp_nist.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nist.c",
                ],
            "crypto\\ec\\ecp_nistp224.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nistp224.c",
                ],
            "crypto\\ec\\ecp_nistp256.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nistp256.c",
                ],
            "crypto\\ec\\ecp_nistp521.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nistp521.c",
                ],
            "crypto\\ec\\ecp_nistputil.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nistputil.c",
                ],
            "crypto\\ec\\ecp_nistz256-x86_64.o" =>
                [
                    "crypto\\ec\\ecp_nistz256-x86_64.s",
                ],
            "crypto\\ec\\ecp_nistz256.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_nistz256.c",
                ],
            "crypto\\ec\\ecp_oct.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_oct.c",
                ],
            "crypto\\ec\\ecp_smpl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecp_smpl.c",
                ],
            "crypto\\ec\\ecx_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ec\\ecx_meth.c",
                ],
            "crypto\\ec\\x25519-x86_64.o" =>
                [
                    "crypto\\ec\\x25519-x86_64.s",
                ],
            "crypto\\engine\\eng_all.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_all.c",
                ],
            "crypto\\engine\\eng_cnf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_cnf.c",
                ],
            "crypto\\engine\\eng_ctrl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_ctrl.c",
                ],
            "crypto\\engine\\eng_dyn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_dyn.c",
                ],
            "crypto\\engine\\eng_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_err.c",
                ],
            "crypto\\engine\\eng_fat.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_fat.c",
                ],
            "crypto\\engine\\eng_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_init.c",
                ],
            "crypto\\engine\\eng_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_lib.c",
                ],
            "crypto\\engine\\eng_list.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_list.c",
                ],
            "crypto\\engine\\eng_openssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_openssl.c",
                ],
            "crypto\\engine\\eng_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_pkey.c",
                ],
            "crypto\\engine\\eng_rdrand.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_rdrand.c",
                ],
            "crypto\\engine\\eng_table.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\eng_table.c",
                ],
            "crypto\\engine\\tb_asnmth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_asnmth.c",
                ],
            "crypto\\engine\\tb_cipher.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_cipher.c",
                ],
            "crypto\\engine\\tb_dh.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_dh.c",
                ],
            "crypto\\engine\\tb_digest.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_digest.c",
                ],
            "crypto\\engine\\tb_dsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_dsa.c",
                ],
            "crypto\\engine\\tb_eckey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_eckey.c",
                ],
            "crypto\\engine\\tb_pkmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_pkmeth.c",
                ],
            "crypto\\engine\\tb_rand.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_rand.c",
                ],
            "crypto\\engine\\tb_rsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\engine\\tb_rsa.c",
                ],
            "crypto\\err\\err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\err\\err.c",
                ],
            "crypto\\err\\err_all.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\err\\err_all.c",
                ],
            "crypto\\err\\err_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\err\\err_prn.c",
                ],
            "crypto\\evp\\bio_b64.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\bio_b64.c",
                ],
            "crypto\\evp\\bio_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\bio_enc.c",
                ],
            "crypto\\evp\\bio_md.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\bio_md.c",
                ],
            "crypto\\evp\\bio_ok.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\bio_ok.c",
                ],
            "crypto\\evp\\c_allc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\c_allc.c",
                ],
            "crypto\\evp\\c_alld.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\c_alld.c",
                ],
            "crypto\\evp\\cmeth_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\cmeth_lib.c",
                ],
            "crypto\\evp\\digest.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\digest.c",
                ],
            "crypto\\evp\\e_aes.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_aes.c",
                ],
            "crypto\\evp\\e_aes_cbc_hmac_sha1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_aes_cbc_hmac_sha1.c",
                ],
            "crypto\\evp\\e_aes_cbc_hmac_sha256.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_aes_cbc_hmac_sha256.c",
                ],
            "crypto\\evp\\e_aria.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_aria.c",
                ],
            "crypto\\evp\\e_bf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_bf.c",
                ],
            "crypto\\evp\\e_camellia.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_camellia.c",
                ],
            "crypto\\evp\\e_cast.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_cast.c",
                ],
            "crypto\\evp\\e_chacha20_poly1305.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_chacha20_poly1305.c",
                ],
            "crypto\\evp\\e_des.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_des.c",
                ],
            "crypto\\evp\\e_des3.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_des3.c",
                ],
            "crypto\\evp\\e_idea.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_idea.c",
                ],
            "crypto\\evp\\e_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_null.c",
                ],
            "crypto\\evp\\e_old.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_old.c",
                ],
            "crypto\\evp\\e_rc2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_rc2.c",
                ],
            "crypto\\evp\\e_rc4.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_rc4.c",
                ],
            "crypto\\evp\\e_rc4_hmac_md5.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_rc4_hmac_md5.c",
                ],
            "crypto\\evp\\e_rc5.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_rc5.c",
                ],
            "crypto\\evp\\e_seed.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_seed.c",
                ],
            "crypto\\evp\\e_sm4.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_sm4.c",
                ],
            "crypto\\evp\\e_xcbc_d.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\e_xcbc_d.c",
                ],
            "crypto\\evp\\encode.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\encode.c",
                ],
            "crypto\\evp\\evp_cnf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_cnf.c",
                ],
            "crypto\\evp\\evp_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_enc.c",
                ],
            "crypto\\evp\\evp_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_err.c",
                ],
            "crypto\\evp\\evp_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_key.c",
                ],
            "crypto\\evp\\evp_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_lib.c",
                ],
            "crypto\\evp\\evp_pbe.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_pbe.c",
                ],
            "crypto\\evp\\evp_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\evp_pkey.c",
                ],
            "crypto\\evp\\m_md2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_md2.c",
                ],
            "crypto\\evp\\m_md4.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_md4.c",
                ],
            "crypto\\evp\\m_md5.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_md5.c",
                ],
            "crypto\\evp\\m_md5_sha1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_md5_sha1.c",
                ],
            "crypto\\evp\\m_mdc2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_mdc2.c",
                ],
            "crypto\\evp\\m_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_null.c",
                ],
            "crypto\\evp\\m_ripemd.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_ripemd.c",
                ],
            "crypto\\evp\\m_sha1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_sha1.c",
                ],
            "crypto\\evp\\m_sha3.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_sha3.c",
                ],
            "crypto\\evp\\m_sigver.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_sigver.c",
                ],
            "crypto\\evp\\m_wp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\m_wp.c",
                ],
            "crypto\\evp\\names.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\names.c",
                ],
            "crypto\\evp\\p5_crpt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p5_crpt.c",
                ],
            "crypto\\evp\\p5_crpt2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p5_crpt2.c",
                ],
            "crypto\\evp\\p_dec.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_dec.c",
                ],
            "crypto\\evp\\p_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_enc.c",
                ],
            "crypto\\evp\\p_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_lib.c",
                ],
            "crypto\\evp\\p_open.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_open.c",
                ],
            "crypto\\evp\\p_seal.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_seal.c",
                ],
            "crypto\\evp\\p_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_sign.c",
                ],
            "crypto\\evp\\p_verify.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\p_verify.c",
                ],
            "crypto\\evp\\pbe_scrypt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\pbe_scrypt.c",
                ],
            "crypto\\evp\\pmeth_fn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\pmeth_fn.c",
                ],
            "crypto\\evp\\pmeth_gn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\pmeth_gn.c",
                ],
            "crypto\\evp\\pmeth_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\evp\\pmeth_lib.c",
                ],
            "crypto\\ex_data.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ex_data.c",
                ],
            "crypto\\getenv.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\getenv.c",
                ],
            "crypto\\hmac\\hm_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\hmac\\hm_ameth.c",
                ],
            "crypto\\hmac\\hm_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\hmac\\hm_pmeth.c",
                ],
            "crypto\\hmac\\hmac.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\hmac\\hmac.c",
                ],
            "crypto\\init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\init.c",
                ],
            "crypto\\kdf\\hkdf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\kdf\\hkdf.c",
                ],
            "crypto\\kdf\\kdf_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\kdf\\kdf_err.c",
                ],
            "crypto\\kdf\\scrypt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\kdf\\scrypt.c",
                ],
            "crypto\\kdf\\tls1_prf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\kdf\\tls1_prf.c",
                ],
            "crypto\\lhash\\lh_stats.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\lhash\\lh_stats.c",
                ],
            "crypto\\lhash\\lhash.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\lhash\\lhash.c",
                ],
            "crypto\\md5\\md5-x86_64.o" =>
                [
                    "crypto\\md5\\md5-x86_64.s",
                ],
            "crypto\\md5\\md5_dgst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\md5_dgst.c",
                ],
            "crypto\\md5\\md5_one.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\md5\\md5_one.c",
                ],
            "crypto\\mem.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\mem.c",
                ],
            "crypto\\mem_dbg.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\mem_dbg.c",
                ],
            "crypto\\mem_sec.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\mem_sec.c",
                ],
            "crypto\\modes\\aesni-gcm-x86_64.o" =>
                [
                    "crypto\\modes\\aesni-gcm-x86_64.s",
                ],
            "crypto\\modes\\cbc128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\cbc128.c",
                ],
            "crypto\\modes\\ccm128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\ccm128.c",
                ],
            "crypto\\modes\\cfb128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\cfb128.c",
                ],
            "crypto\\modes\\ctr128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\ctr128.c",
                ],
            "crypto\\modes\\cts128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\cts128.c",
                ],
            "crypto\\modes\\gcm128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\gcm128.c",
                ],
            "crypto\\modes\\ghash-x86_64.o" =>
                [
                    "crypto\\modes\\ghash-x86_64.s",
                ],
            "crypto\\modes\\ocb128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\ocb128.c",
                ],
            "crypto\\modes\\ofb128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\ofb128.c",
                ],
            "crypto\\modes\\wrap128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\wrap128.c",
                ],
            "crypto\\modes\\xts128.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\modes\\xts128.c",
                ],
            "crypto\\o_dir.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_dir.c",
                ],
            "crypto\\o_fips.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_fips.c",
                ],
            "crypto\\o_fopen.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_fopen.c",
                ],
            "crypto\\o_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_init.c",
                ],
            "crypto\\o_str.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_str.c",
                ],
            "crypto\\o_time.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\o_time.c",
                ],
            "crypto\\objects\\o_names.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\o_names.c",
                ],
            "crypto\\objects\\obj_dat.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\obj_dat.c",
                ],
            "crypto\\objects\\obj_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\obj_err.c",
                ],
            "crypto\\objects\\obj_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\obj_lib.c",
                ],
            "crypto\\objects\\obj_xref.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\objects\\obj_xref.c",
                ],
            "crypto\\ocsp\\ocsp_asn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_asn.c",
                ],
            "crypto\\ocsp\\ocsp_cl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_cl.c",
                ],
            "crypto\\ocsp\\ocsp_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_err.c",
                ],
            "crypto\\ocsp\\ocsp_ext.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_ext.c",
                ],
            "crypto\\ocsp\\ocsp_ht.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_ht.c",
                ],
            "crypto\\ocsp\\ocsp_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_lib.c",
                ],
            "crypto\\ocsp\\ocsp_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_prn.c",
                ],
            "crypto\\ocsp\\ocsp_srv.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_srv.c",
                ],
            "crypto\\ocsp\\ocsp_vfy.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\ocsp_vfy.c",
                ],
            "crypto\\ocsp\\v3_ocsp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ocsp\\v3_ocsp.c",
                ],
            "crypto\\pem\\pem_all.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_all.c",
                ],
            "crypto\\pem\\pem_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_err.c",
                ],
            "crypto\\pem\\pem_info.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_info.c",
                ],
            "crypto\\pem\\pem_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_lib.c",
                ],
            "crypto\\pem\\pem_oth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_oth.c",
                ],
            "crypto\\pem\\pem_pk8.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_pk8.c",
                ],
            "crypto\\pem\\pem_pkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_pkey.c",
                ],
            "crypto\\pem\\pem_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_sign.c",
                ],
            "crypto\\pem\\pem_x509.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_x509.c",
                ],
            "crypto\\pem\\pem_xaux.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pem_xaux.c",
                ],
            "crypto\\pem\\pvkfmt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pem\\pvkfmt.c",
                ],
            "crypto\\pkcs12\\p12_add.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_add.c",
                ],
            "crypto\\pkcs12\\p12_asn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_asn.c",
                ],
            "crypto\\pkcs12\\p12_attr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_attr.c",
                ],
            "crypto\\pkcs12\\p12_crpt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_crpt.c",
                ],
            "crypto\\pkcs12\\p12_crt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_crt.c",
                ],
            "crypto\\pkcs12\\p12_decr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_decr.c",
                ],
            "crypto\\pkcs12\\p12_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_init.c",
                ],
            "crypto\\pkcs12\\p12_key.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_key.c",
                ],
            "crypto\\pkcs12\\p12_kiss.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_kiss.c",
                ],
            "crypto\\pkcs12\\p12_mutl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_mutl.c",
                ],
            "crypto\\pkcs12\\p12_npas.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_npas.c",
                ],
            "crypto\\pkcs12\\p12_p8d.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_p8d.c",
                ],
            "crypto\\pkcs12\\p12_p8e.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_p8e.c",
                ],
            "crypto\\pkcs12\\p12_sbag.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_sbag.c",
                ],
            "crypto\\pkcs12\\p12_utl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\p12_utl.c",
                ],
            "crypto\\pkcs12\\pk12err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs12\\pk12err.c",
                ],
            "crypto\\pkcs7\\bio_pk7.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\bio_pk7.c",
                ],
            "crypto\\pkcs7\\pk7_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_asn1.c",
                ],
            "crypto\\pkcs7\\pk7_attr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_attr.c",
                ],
            "crypto\\pkcs7\\pk7_doit.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_doit.c",
                ],
            "crypto\\pkcs7\\pk7_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_lib.c",
                ],
            "crypto\\pkcs7\\pk7_mime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_mime.c",
                ],
            "crypto\\pkcs7\\pk7_smime.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pk7_smime.c",
                ],
            "crypto\\pkcs7\\pkcs7err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\pkcs7\\pkcs7err.c",
                ],
            "crypto\\poly1305\\poly1305-x86_64.o" =>
                [
                    "crypto\\poly1305\\poly1305-x86_64.s",
                ],
            "crypto\\poly1305\\poly1305.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\poly1305.c",
                ],
            "crypto\\poly1305\\poly1305_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\poly1305_ameth.c",
                ],
            "crypto\\poly1305\\poly1305_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\poly1305\\poly1305_pmeth.c",
                ],
            "crypto\\rand\\drbg_ctr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\drbg_ctr.c",
                ],
            "crypto\\rand\\drbg_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\drbg_lib.c",
                ],
            "crypto\\rand\\rand_egd.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_egd.c",
                ],
            "crypto\\rand\\rand_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_err.c",
                ],
            "crypto\\rand\\rand_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_lib.c",
                ],
            "crypto\\rand\\rand_unix.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_unix.c",
                ],
            "crypto\\rand\\rand_vms.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_vms.c",
                ],
            "crypto\\rand\\rand_win.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\rand_win.c",
                ],
            "crypto\\rand\\randfile.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rand\\randfile.c",
                ],
            "crypto\\rsa\\rsa_ameth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_ameth.c",
                ],
            "crypto\\rsa\\rsa_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_asn1.c",
                ],
            "crypto\\rsa\\rsa_chk.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_chk.c",
                ],
            "crypto\\rsa\\rsa_crpt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_crpt.c",
                ],
            "crypto\\rsa\\rsa_depr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_depr.c",
                ],
            "crypto\\rsa\\rsa_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_err.c",
                ],
            "crypto\\rsa\\rsa_gen.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_gen.c",
                ],
            "crypto\\rsa\\rsa_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_lib.c",
                ],
            "crypto\\rsa\\rsa_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_meth.c",
                ],
            "crypto\\rsa\\rsa_mp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_mp.c",
                ],
            "crypto\\rsa\\rsa_none.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_none.c",
                ],
            "crypto\\rsa\\rsa_oaep.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_oaep.c",
                ],
            "crypto\\rsa\\rsa_ossl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_ossl.c",
                ],
            "crypto\\rsa\\rsa_pk1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_pk1.c",
                ],
            "crypto\\rsa\\rsa_pmeth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_pmeth.c",
                ],
            "crypto\\rsa\\rsa_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_prn.c",
                ],
            "crypto\\rsa\\rsa_pss.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_pss.c",
                ],
            "crypto\\rsa\\rsa_saos.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_saos.c",
                ],
            "crypto\\rsa\\rsa_sign.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_sign.c",
                ],
            "crypto\\rsa\\rsa_ssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_ssl.c",
                ],
            "crypto\\rsa\\rsa_x931.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_x931.c",
                ],
            "crypto\\rsa\\rsa_x931g.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\rsa\\rsa_x931g.c",
                ],
            "crypto\\sha\\keccak1600-x86_64.o" =>
                [
                    "crypto\\sha\\keccak1600-x86_64.s",
                ],
            "crypto\\sha\\sha1-mb-x86_64.o" =>
                [
                    "crypto\\sha\\sha1-mb-x86_64.s",
                ],
            "crypto\\sha\\sha1-x86_64.o" =>
                [
                    "crypto\\sha\\sha1-x86_64.s",
                ],
            "crypto\\sha\\sha1_one.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\sha1_one.c",
                ],
            "crypto\\sha\\sha1dgst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\sha1dgst.c",
                ],
            "crypto\\sha\\sha256-mb-x86_64.o" =>
                [
                    "crypto\\sha\\sha256-mb-x86_64.s",
                ],
            "crypto\\sha\\sha256-x86_64.o" =>
                [
                    "crypto\\sha\\sha256-x86_64.s",
                ],
            "crypto\\sha\\sha256.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\sha256.c",
                ],
            "crypto\\sha\\sha512-x86_64.o" =>
                [
                    "crypto\\sha\\sha512-x86_64.s",
                ],
            "crypto\\sha\\sha512.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\sha\\sha512.c",
                ],
            "crypto\\stack\\stack.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\stack\\stack.c",
                ],
            "crypto\\store\\loader_file.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\loader_file.c",
                ],
            "crypto\\store\\store_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\store_err.c",
                ],
            "crypto\\store\\store_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\store_init.c",
                ],
            "crypto\\store\\store_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\store_lib.c",
                ],
            "crypto\\store\\store_register.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\store_register.c",
                ],
            "crypto\\store\\store_strings.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\store\\store_strings.c",
                ],
            "crypto\\threads_none.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\threads_none.c",
                ],
            "crypto\\threads_pthread.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\threads_pthread.c",
                ],
            "crypto\\threads_win.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\threads_win.c",
                ],
            "crypto\\txt_db\\txt_db.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\txt_db\\txt_db.c",
                ],
            "crypto\\ui\\ui_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\ui_err.c",
                ],
            "crypto\\ui\\ui_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\ui_lib.c",
                ],
            "crypto\\ui\\ui_null.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\ui_null.c",
                ],
            "crypto\\ui\\ui_openssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\ui_openssl.c",
                ],
            "crypto\\ui\\ui_util.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\ui\\ui_util.c",
                ],
            "crypto\\uid.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\uid.c",
                ],
            "crypto\\x509\\by_dir.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\by_dir.c",
                ],
            "crypto\\x509\\by_file.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\by_file.c",
                ],
            "crypto\\x509\\t_crl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\t_crl.c",
                ],
            "crypto\\x509\\t_req.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\t_req.c",
                ],
            "crypto\\x509\\t_x509.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\t_x509.c",
                ],
            "crypto\\x509\\x509_att.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_att.c",
                ],
            "crypto\\x509\\x509_cmp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_cmp.c",
                ],
            "crypto\\x509\\x509_d2.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_d2.c",
                ],
            "crypto\\x509\\x509_def.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_def.c",
                ],
            "crypto\\x509\\x509_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_err.c",
                ],
            "crypto\\x509\\x509_ext.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_ext.c",
                ],
            "crypto\\x509\\x509_lu.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_lu.c",
                ],
            "crypto\\x509\\x509_meth.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_meth.c",
                ],
            "crypto\\x509\\x509_obj.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_obj.c",
                ],
            "crypto\\x509\\x509_r2x.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_r2x.c",
                ],
            "crypto\\x509\\x509_req.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_req.c",
                ],
            "crypto\\x509\\x509_set.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_set.c",
                ],
            "crypto\\x509\\x509_trs.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_trs.c",
                ],
            "crypto\\x509\\x509_txt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_txt.c",
                ],
            "crypto\\x509\\x509_v3.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_v3.c",
                ],
            "crypto\\x509\\x509_vfy.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_vfy.c",
                ],
            "crypto\\x509\\x509_vpm.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509_vpm.c",
                ],
            "crypto\\x509\\x509cset.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509cset.c",
                ],
            "crypto\\x509\\x509name.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509name.c",
                ],
            "crypto\\x509\\x509rset.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509rset.c",
                ],
            "crypto\\x509\\x509spki.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509spki.c",
                ],
            "crypto\\x509\\x509type.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x509type.c",
                ],
            "crypto\\x509\\x_all.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_all.c",
                ],
            "crypto\\x509\\x_attrib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_attrib.c",
                ],
            "crypto\\x509\\x_crl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_crl.c",
                ],
            "crypto\\x509\\x_exten.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_exten.c",
                ],
            "crypto\\x509\\x_name.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_name.c",
                ],
            "crypto\\x509\\x_pubkey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_pubkey.c",
                ],
            "crypto\\x509\\x_req.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_req.c",
                ],
            "crypto\\x509\\x_x509.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_x509.c",
                ],
            "crypto\\x509\\x_x509a.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509\\x_x509a.c",
                ],
            "crypto\\x509v3\\pcy_cache.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_cache.c",
                ],
            "crypto\\x509v3\\pcy_data.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_data.c",
                ],
            "crypto\\x509v3\\pcy_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_lib.c",
                ],
            "crypto\\x509v3\\pcy_map.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_map.c",
                ],
            "crypto\\x509v3\\pcy_node.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_node.c",
                ],
            "crypto\\x509v3\\pcy_tree.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\pcy_tree.c",
                ],
            "crypto\\x509v3\\v3_addr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_addr.c",
                ],
            "crypto\\x509v3\\v3_admis.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_admis.c",
                ],
            "crypto\\x509v3\\v3_akey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_akey.c",
                ],
            "crypto\\x509v3\\v3_akeya.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_akeya.c",
                ],
            "crypto\\x509v3\\v3_alt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_alt.c",
                ],
            "crypto\\x509v3\\v3_asid.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_asid.c",
                ],
            "crypto\\x509v3\\v3_bcons.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_bcons.c",
                ],
            "crypto\\x509v3\\v3_bitst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_bitst.c",
                ],
            "crypto\\x509v3\\v3_conf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_conf.c",
                ],
            "crypto\\x509v3\\v3_cpols.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_cpols.c",
                ],
            "crypto\\x509v3\\v3_crld.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_crld.c",
                ],
            "crypto\\x509v3\\v3_enum.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_enum.c",
                ],
            "crypto\\x509v3\\v3_extku.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_extku.c",
                ],
            "crypto\\x509v3\\v3_genn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_genn.c",
                ],
            "crypto\\x509v3\\v3_ia5.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_ia5.c",
                ],
            "crypto\\x509v3\\v3_info.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_info.c",
                ],
            "crypto\\x509v3\\v3_int.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_int.c",
                ],
            "crypto\\x509v3\\v3_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_lib.c",
                ],
            "crypto\\x509v3\\v3_ncons.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_ncons.c",
                ],
            "crypto\\x509v3\\v3_pci.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_pci.c",
                ],
            "crypto\\x509v3\\v3_pcia.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_pcia.c",
                ],
            "crypto\\x509v3\\v3_pcons.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_pcons.c",
                ],
            "crypto\\x509v3\\v3_pku.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_pku.c",
                ],
            "crypto\\x509v3\\v3_pmaps.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_pmaps.c",
                ],
            "crypto\\x509v3\\v3_prn.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_prn.c",
                ],
            "crypto\\x509v3\\v3_purp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_purp.c",
                ],
            "crypto\\x509v3\\v3_skey.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_skey.c",
                ],
            "crypto\\x509v3\\v3_sxnet.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_sxnet.c",
                ],
            "crypto\\x509v3\\v3_tlsf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_tlsf.c",
                ],
            "crypto\\x509v3\\v3_utl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3_utl.c",
                ],
            "crypto\\x509v3\\v3err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\crypto\\x509v3\\v3err.c",
                ],
            "crypto\\x86_64cpuid.o" =>
                [
                    "crypto\\x86_64cpuid.s",
                ],
            "engines\\e_capi.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\engines\\e_capi.c",
                ],
            "engines\\e_padlock-x86_64.o" =>
                [
                    "engines\\e_padlock-x86_64.s",
                ],
            "engines\\e_padlock.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\engines\\e_padlock.c",
                ],
            "libcrypto" =>
                [
                    "crypto\\aes\\aes_cbc.o",
                    "crypto\\aes\\aes_cfb.o",
                    "crypto\\aes\\aes_core.o",
                    "crypto\\aes\\aes_ecb.o",
                    "crypto\\aes\\aes_ige.o",
                    "crypto\\aes\\aes_misc.o",
                    "crypto\\aes\\aes_ofb.o",
                    "crypto\\aes\\aes_wrap.o",
                    "crypto\\aes\\aesni-mb-x86_64.o",
                    "crypto\\aes\\aesni-sha1-x86_64.o",
                    "crypto\\aes\\aesni-sha256-x86_64.o",
                    "crypto\\aes\\aesni-x86_64.o",
                    "crypto\\aes\\vpaes-x86_64.o",
                    "crypto\\asn1\\a_bitstr.o",
                    "crypto\\asn1\\a_d2i_fp.o",
                    "crypto\\asn1\\a_digest.o",
                    "crypto\\asn1\\a_dup.o",
                    "crypto\\asn1\\a_gentm.o",
                    "crypto\\asn1\\a_i2d_fp.o",
                    "crypto\\asn1\\a_int.o",
                    "crypto\\asn1\\a_mbstr.o",
                    "crypto\\asn1\\a_object.o",
                    "crypto\\asn1\\a_octet.o",
                    "crypto\\asn1\\a_print.o",
                    "crypto\\asn1\\a_sign.o",
                    "crypto\\asn1\\a_strex.o",
                    "crypto\\asn1\\a_strnid.o",
                    "crypto\\asn1\\a_time.o",
                    "crypto\\asn1\\a_type.o",
                    "crypto\\asn1\\a_utctm.o",
                    "crypto\\asn1\\a_utf8.o",
                    "crypto\\asn1\\a_verify.o",
                    "crypto\\asn1\\ameth_lib.o",
                    "crypto\\asn1\\asn1_err.o",
                    "crypto\\asn1\\asn1_gen.o",
                    "crypto\\asn1\\asn1_item_list.o",
                    "crypto\\asn1\\asn1_lib.o",
                    "crypto\\asn1\\asn1_par.o",
                    "crypto\\asn1\\asn_mime.o",
                    "crypto\\asn1\\asn_moid.o",
                    "crypto\\asn1\\asn_mstbl.o",
                    "crypto\\asn1\\asn_pack.o",
                    "crypto\\asn1\\bio_asn1.o",
                    "crypto\\asn1\\bio_ndef.o",
                    "crypto\\asn1\\d2i_pr.o",
                    "crypto\\asn1\\d2i_pu.o",
                    "crypto\\asn1\\evp_asn1.o",
                    "crypto\\asn1\\f_int.o",
                    "crypto\\asn1\\f_string.o",
                    "crypto\\asn1\\i2d_pr.o",
                    "crypto\\asn1\\i2d_pu.o",
                    "crypto\\asn1\\n_pkey.o",
                    "crypto\\asn1\\nsseq.o",
                    "crypto\\asn1\\p5_pbe.o",
                    "crypto\\asn1\\p5_pbev2.o",
                    "crypto\\asn1\\p5_scrypt.o",
                    "crypto\\asn1\\p8_pkey.o",
                    "crypto\\asn1\\t_bitst.o",
                    "crypto\\asn1\\t_pkey.o",
                    "crypto\\asn1\\t_spki.o",
                    "crypto\\asn1\\tasn_dec.o",
                    "crypto\\asn1\\tasn_enc.o",
                    "crypto\\asn1\\tasn_fre.o",
                    "crypto\\asn1\\tasn_new.o",
                    "crypto\\asn1\\tasn_prn.o",
                    "crypto\\asn1\\tasn_scn.o",
                    "crypto\\asn1\\tasn_typ.o",
                    "crypto\\asn1\\tasn_utl.o",
                    "crypto\\asn1\\x_algor.o",
                    "crypto\\asn1\\x_bignum.o",
                    "crypto\\asn1\\x_info.o",
                    "crypto\\asn1\\x_int64.o",
                    "crypto\\asn1\\x_long.o",
                    "crypto\\asn1\\x_pkey.o",
                    "crypto\\asn1\\x_sig.o",
                    "crypto\\asn1\\x_spki.o",
                    "crypto\\asn1\\x_val.o",
                    "crypto\\async\\arch\\async_null.o",
                    "crypto\\async\\arch\\async_posix.o",
                    "crypto\\async\\arch\\async_win.o",
                    "crypto\\async\\async.o",
                    "crypto\\async\\async_err.o",
                    "crypto\\async\\async_wait.o",
                    "crypto\\bio\\b_addr.o",
                    "crypto\\bio\\b_dump.o",
                    "crypto\\bio\\b_print.o",
                    "crypto\\bio\\b_sock.o",
                    "crypto\\bio\\b_sock2.o",
                    "crypto\\bio\\bf_buff.o",
                    "crypto\\bio\\bf_lbuf.o",
                    "crypto\\bio\\bf_nbio.o",
                    "crypto\\bio\\bf_null.o",
                    "crypto\\bio\\bio_cb.o",
                    "crypto\\bio\\bio_err.o",
                    "crypto\\bio\\bio_lib.o",
                    "crypto\\bio\\bio_meth.o",
                    "crypto\\bio\\bss_acpt.o",
                    "crypto\\bio\\bss_bio.o",
                    "crypto\\bio\\bss_conn.o",
                    "crypto\\bio\\bss_dgram.o",
                    "crypto\\bio\\bss_fd.o",
                    "crypto\\bio\\bss_file.o",
                    "crypto\\bio\\bss_log.o",
                    "crypto\\bio\\bss_mem.o",
                    "crypto\\bio\\bss_null.o",
                    "crypto\\bio\\bss_sock.o",
                    "crypto\\bn\\bn_add.o",
                    "crypto\\bn\\bn_asm.o",
                    "crypto\\bn\\bn_blind.o",
                    "crypto\\bn\\bn_const.o",
                    "crypto\\bn\\bn_ctx.o",
                    "crypto\\bn\\bn_depr.o",
                    "crypto\\bn\\bn_dh.o",
                    "crypto\\bn\\bn_div.o",
                    "crypto\\bn\\bn_err.o",
                    "crypto\\bn\\bn_exp.o",
                    "crypto\\bn\\bn_exp2.o",
                    "crypto\\bn\\bn_gcd.o",
                    "crypto\\bn\\bn_gf2m.o",
                    "crypto\\bn\\bn_intern.o",
                    "crypto\\bn\\bn_kron.o",
                    "crypto\\bn\\bn_lib.o",
                    "crypto\\bn\\bn_mod.o",
                    "crypto\\bn\\bn_mont.o",
                    "crypto\\bn\\bn_mpi.o",
                    "crypto\\bn\\bn_mul.o",
                    "crypto\\bn\\bn_nist.o",
                    "crypto\\bn\\bn_prime.o",
                    "crypto\\bn\\bn_print.o",
                    "crypto\\bn\\bn_rand.o",
                    "crypto\\bn\\bn_recp.o",
                    "crypto\\bn\\bn_shift.o",
                    "crypto\\bn\\bn_sqr.o",
                    "crypto\\bn\\bn_sqrt.o",
                    "crypto\\bn\\bn_srp.o",
                    "crypto\\bn\\bn_word.o",
                    "crypto\\bn\\bn_x931p.o",
                    "crypto\\bn\\rsaz-avx2.o",
                    "crypto\\bn\\rsaz-x86_64.o",
                    "crypto\\bn\\rsaz_exp.o",
                    "crypto\\bn\\x86_64-gf2m.o",
                    "crypto\\bn\\x86_64-mont.o",
                    "crypto\\bn\\x86_64-mont5.o",
                    "crypto\\buffer\\buf_err.o",
                    "crypto\\buffer\\buffer.o",
                    "crypto\\chacha\\chacha-x86_64.o",
                    "crypto\\cmac\\cm_ameth.o",
                    "crypto\\cmac\\cm_pmeth.o",
                    "crypto\\cmac\\cmac.o",
                    "crypto\\conf\\conf_api.o",
                    "crypto\\conf\\conf_def.o",
                    "crypto\\conf\\conf_err.o",
                    "crypto\\conf\\conf_lib.o",
                    "crypto\\conf\\conf_mall.o",
                    "crypto\\conf\\conf_mod.o",
                    "crypto\\conf\\conf_sap.o",
                    "crypto\\conf\\conf_ssl.o",
                    "crypto\\cpt_err.o",
                    "crypto\\cryptlib.o",
                    "crypto\\ctype.o",
                    "crypto\\cversion.o",
                    "crypto\\des\\cbc_cksm.o",
                    "crypto\\des\\cbc_enc.o",
                    "crypto\\des\\cfb64ede.o",
                    "crypto\\des\\cfb64enc.o",
                    "crypto\\des\\cfb_enc.o",
                    "crypto\\des\\des_enc.o",
                    "crypto\\des\\ecb3_enc.o",
                    "crypto\\des\\ecb_enc.o",
                    "crypto\\des\\fcrypt.o",
                    "crypto\\des\\fcrypt_b.o",
                    "crypto\\des\\ofb64ede.o",
                    "crypto\\des\\ofb64enc.o",
                    "crypto\\des\\ofb_enc.o",
                    "crypto\\des\\pcbc_enc.o",
                    "crypto\\des\\qud_cksm.o",
                    "crypto\\des\\rand_key.o",
                    "crypto\\des\\set_key.o",
                    "crypto\\des\\str2key.o",
                    "crypto\\des\\xcbc_enc.o",
                    "crypto\\dh\\dh_ameth.o",
                    "crypto\\dh\\dh_asn1.o",
                    "crypto\\dh\\dh_check.o",
                    "crypto\\dh\\dh_depr.o",
                    "crypto\\dh\\dh_err.o",
                    "crypto\\dh\\dh_gen.o",
                    "crypto\\dh\\dh_kdf.o",
                    "crypto\\dh\\dh_key.o",
                    "crypto\\dh\\dh_lib.o",
                    "crypto\\dh\\dh_meth.o",
                    "crypto\\dh\\dh_pmeth.o",
                    "crypto\\dh\\dh_prn.o",
                    "crypto\\dh\\dh_rfc5114.o",
                    "crypto\\dh\\dh_rfc7919.o",
                    "crypto\\dsa\\dsa_ameth.o",
                    "crypto\\dsa\\dsa_asn1.o",
                    "crypto\\dsa\\dsa_depr.o",
                    "crypto\\dsa\\dsa_err.o",
                    "crypto\\dsa\\dsa_gen.o",
                    "crypto\\dsa\\dsa_key.o",
                    "crypto\\dsa\\dsa_lib.o",
                    "crypto\\dsa\\dsa_meth.o",
                    "crypto\\dsa\\dsa_ossl.o",
                    "crypto\\dsa\\dsa_pmeth.o",
                    "crypto\\dsa\\dsa_prn.o",
                    "crypto\\dsa\\dsa_sign.o",
                    "crypto\\dsa\\dsa_vrf.o",
                    "crypto\\dso\\dso_dl.o",
                    "crypto\\dso\\dso_dlfcn.o",
                    "crypto\\dso\\dso_err.o",
                    "crypto\\dso\\dso_lib.o",
                    "crypto\\dso\\dso_openssl.o",
                    "crypto\\dso\\dso_vms.o",
                    "crypto\\dso\\dso_win32.o",
                    "crypto\\ebcdic.o",
                    "crypto\\ec\\curve25519.o",
                    "crypto\\ec\\curve448\\arch_32\\f_impl.o",
                    "crypto\\ec\\curve448\\curve448.o",
                    "crypto\\ec\\curve448\\curve448_tables.o",
                    "crypto\\ec\\curve448\\eddsa.o",
                    "crypto\\ec\\curve448\\f_generic.o",
                    "crypto\\ec\\curve448\\scalar.o",
                    "crypto\\ec\\ec2_oct.o",
                    "crypto\\ec\\ec2_smpl.o",
                    "crypto\\ec\\ec_ameth.o",
                    "crypto\\ec\\ec_asn1.o",
                    "crypto\\ec\\ec_check.o",
                    "crypto\\ec\\ec_curve.o",
                    "crypto\\ec\\ec_cvt.o",
                    "crypto\\ec\\ec_err.o",
                    "crypto\\ec\\ec_key.o",
                    "crypto\\ec\\ec_kmeth.o",
                    "crypto\\ec\\ec_lib.o",
                    "crypto\\ec\\ec_mult.o",
                    "crypto\\ec\\ec_oct.o",
                    "crypto\\ec\\ec_pmeth.o",
                    "crypto\\ec\\ec_print.o",
                    "crypto\\ec\\ecdh_kdf.o",
                    "crypto\\ec\\ecdh_ossl.o",
                    "crypto\\ec\\ecdsa_ossl.o",
                    "crypto\\ec\\ecdsa_sign.o",
                    "crypto\\ec\\ecdsa_vrf.o",
                    "crypto\\ec\\eck_prn.o",
                    "crypto\\ec\\ecp_mont.o",
                    "crypto\\ec\\ecp_nist.o",
                    "crypto\\ec\\ecp_nistp224.o",
                    "crypto\\ec\\ecp_nistp256.o",
                    "crypto\\ec\\ecp_nistp521.o",
                    "crypto\\ec\\ecp_nistputil.o",
                    "crypto\\ec\\ecp_nistz256-x86_64.o",
                    "crypto\\ec\\ecp_nistz256.o",
                    "crypto\\ec\\ecp_oct.o",
                    "crypto\\ec\\ecp_smpl.o",
                    "crypto\\ec\\ecx_meth.o",
                    "crypto\\ec\\x25519-x86_64.o",
                    "crypto\\engine\\eng_all.o",
                    "crypto\\engine\\eng_cnf.o",
                    "crypto\\engine\\eng_ctrl.o",
                    "crypto\\engine\\eng_dyn.o",
                    "crypto\\engine\\eng_err.o",
                    "crypto\\engine\\eng_fat.o",
                    "crypto\\engine\\eng_init.o",
                    "crypto\\engine\\eng_lib.o",
                    "crypto\\engine\\eng_list.o",
                    "crypto\\engine\\eng_openssl.o",
                    "crypto\\engine\\eng_pkey.o",
                    "crypto\\engine\\eng_rdrand.o",
                    "crypto\\engine\\eng_table.o",
                    "crypto\\engine\\tb_asnmth.o",
                    "crypto\\engine\\tb_cipher.o",
                    "crypto\\engine\\tb_dh.o",
                    "crypto\\engine\\tb_digest.o",
                    "crypto\\engine\\tb_dsa.o",
                    "crypto\\engine\\tb_eckey.o",
                    "crypto\\engine\\tb_pkmeth.o",
                    "crypto\\engine\\tb_rand.o",
                    "crypto\\engine\\tb_rsa.o",
                    "crypto\\err\\err.o",
                    "crypto\\err\\err_all.o",
                    "crypto\\err\\err_prn.o",
                    "crypto\\evp\\bio_b64.o",
                    "crypto\\evp\\bio_enc.o",
                    "crypto\\evp\\bio_md.o",
                    "crypto\\evp\\bio_ok.o",
                    "crypto\\evp\\c_allc.o",
                    "crypto\\evp\\c_alld.o",
                    "crypto\\evp\\cmeth_lib.o",
                    "crypto\\evp\\digest.o",
                    "crypto\\evp\\e_aes.o",
                    "crypto\\evp\\e_aes_cbc_hmac_sha1.o",
                    "crypto\\evp\\e_aes_cbc_hmac_sha256.o",
                    "crypto\\evp\\e_aria.o",
                    "crypto\\evp\\e_bf.o",
                    "crypto\\evp\\e_camellia.o",
                    "crypto\\evp\\e_cast.o",
                    "crypto\\evp\\e_chacha20_poly1305.o",
                    "crypto\\evp\\e_des.o",
                    "crypto\\evp\\e_des3.o",
                    "crypto\\evp\\e_idea.o",
                    "crypto\\evp\\e_null.o",
                    "crypto\\evp\\e_old.o",
                    "crypto\\evp\\e_rc2.o",
                    "crypto\\evp\\e_rc4.o",
                    "crypto\\evp\\e_rc4_hmac_md5.o",
                    "crypto\\evp\\e_rc5.o",
                    "crypto\\evp\\e_seed.o",
                    "crypto\\evp\\e_sm4.o",
                    "crypto\\evp\\e_xcbc_d.o",
                    "crypto\\evp\\encode.o",
                    "crypto\\evp\\evp_cnf.o",
                    "crypto\\evp\\evp_enc.o",
                    "crypto\\evp\\evp_err.o",
                    "crypto\\evp\\evp_key.o",
                    "crypto\\evp\\evp_lib.o",
                    "crypto\\evp\\evp_pbe.o",
                    "crypto\\evp\\evp_pkey.o",
                    "crypto\\evp\\m_md2.o",
                    "crypto\\evp\\m_md4.o",
                    "crypto\\evp\\m_md5.o",
                    "crypto\\evp\\m_md5_sha1.o",
                    "crypto\\evp\\m_mdc2.o",
                    "crypto\\evp\\m_null.o",
                    "crypto\\evp\\m_ripemd.o",
                    "crypto\\evp\\m_sha1.o",
                    "crypto\\evp\\m_sha3.o",
                    "crypto\\evp\\m_sigver.o",
                    "crypto\\evp\\m_wp.o",
                    "crypto\\evp\\names.o",
                    "crypto\\evp\\p5_crpt.o",
                    "crypto\\evp\\p5_crpt2.o",
                    "crypto\\evp\\p_dec.o",
                    "crypto\\evp\\p_enc.o",
                    "crypto\\evp\\p_lib.o",
                    "crypto\\evp\\p_open.o",
                    "crypto\\evp\\p_seal.o",
                    "crypto\\evp\\p_sign.o",
                    "crypto\\evp\\p_verify.o",
                    "crypto\\evp\\pbe_scrypt.o",
                    "crypto\\evp\\pmeth_fn.o",
                    "crypto\\evp\\pmeth_gn.o",
                    "crypto\\evp\\pmeth_lib.o",
                    "crypto\\ex_data.o",
                    "crypto\\getenv.o",
                    "crypto\\hmac\\hm_ameth.o",
                    "crypto\\hmac\\hm_pmeth.o",
                    "crypto\\hmac\\hmac.o",
                    "crypto\\init.o",
                    "crypto\\kdf\\hkdf.o",
                    "crypto\\kdf\\kdf_err.o",
                    "crypto\\kdf\\scrypt.o",
                    "crypto\\kdf\\tls1_prf.o",
                    "crypto\\lhash\\lh_stats.o",
                    "crypto\\lhash\\lhash.o",
                    "crypto\\md5\\md5-x86_64.o",
                    "crypto\\md5\\md5_dgst.o",
                    "crypto\\md5\\md5_one.o",
                    "crypto\\mem.o",
                    "crypto\\mem_dbg.o",
                    "crypto\\mem_sec.o",
                    "crypto\\modes\\aesni-gcm-x86_64.o",
                    "crypto\\modes\\cbc128.o",
                    "crypto\\modes\\ccm128.o",
                    "crypto\\modes\\cfb128.o",
                    "crypto\\modes\\ctr128.o",
                    "crypto\\modes\\cts128.o",
                    "crypto\\modes\\gcm128.o",
                    "crypto\\modes\\ghash-x86_64.o",
                    "crypto\\modes\\ocb128.o",
                    "crypto\\modes\\ofb128.o",
                    "crypto\\modes\\wrap128.o",
                    "crypto\\modes\\xts128.o",
                    "crypto\\o_dir.o",
                    "crypto\\o_fips.o",
                    "crypto\\o_fopen.o",
                    "crypto\\o_init.o",
                    "crypto\\o_str.o",
                    "crypto\\o_time.o",
                    "crypto\\objects\\o_names.o",
                    "crypto\\objects\\obj_dat.o",
                    "crypto\\objects\\obj_err.o",
                    "crypto\\objects\\obj_lib.o",
                    "crypto\\objects\\obj_xref.o",
                    "crypto\\ocsp\\ocsp_asn.o",
                    "crypto\\ocsp\\ocsp_cl.o",
                    "crypto\\ocsp\\ocsp_err.o",
                    "crypto\\ocsp\\ocsp_ext.o",
                    "crypto\\ocsp\\ocsp_ht.o",
                    "crypto\\ocsp\\ocsp_lib.o",
                    "crypto\\ocsp\\ocsp_prn.o",
                    "crypto\\ocsp\\ocsp_srv.o",
                    "crypto\\ocsp\\ocsp_vfy.o",
                    "crypto\\ocsp\\v3_ocsp.o",
                    "crypto\\pem\\pem_all.o",
                    "crypto\\pem\\pem_err.o",
                    "crypto\\pem\\pem_info.o",
                    "crypto\\pem\\pem_lib.o",
                    "crypto\\pem\\pem_oth.o",
                    "crypto\\pem\\pem_pk8.o",
                    "crypto\\pem\\pem_pkey.o",
                    "crypto\\pem\\pem_sign.o",
                    "crypto\\pem\\pem_x509.o",
                    "crypto\\pem\\pem_xaux.o",
                    "crypto\\pem\\pvkfmt.o",
                    "crypto\\pkcs12\\p12_add.o",
                    "crypto\\pkcs12\\p12_asn.o",
                    "crypto\\pkcs12\\p12_attr.o",
                    "crypto\\pkcs12\\p12_crpt.o",
                    "crypto\\pkcs12\\p12_crt.o",
                    "crypto\\pkcs12\\p12_decr.o",
                    "crypto\\pkcs12\\p12_init.o",
                    "crypto\\pkcs12\\p12_key.o",
                    "crypto\\pkcs12\\p12_kiss.o",
                    "crypto\\pkcs12\\p12_mutl.o",
                    "crypto\\pkcs12\\p12_npas.o",
                    "crypto\\pkcs12\\p12_p8d.o",
                    "crypto\\pkcs12\\p12_p8e.o",
                    "crypto\\pkcs12\\p12_sbag.o",
                    "crypto\\pkcs12\\p12_utl.o",
                    "crypto\\pkcs12\\pk12err.o",
                    "crypto\\pkcs7\\bio_pk7.o",
                    "crypto\\pkcs7\\pk7_asn1.o",
                    "crypto\\pkcs7\\pk7_attr.o",
                    "crypto\\pkcs7\\pk7_doit.o",
                    "crypto\\pkcs7\\pk7_lib.o",
                    "crypto\\pkcs7\\pk7_mime.o",
                    "crypto\\pkcs7\\pk7_smime.o",
                    "crypto\\pkcs7\\pkcs7err.o",
                    "crypto\\poly1305\\poly1305-x86_64.o",
                    "crypto\\poly1305\\poly1305.o",
                    "crypto\\poly1305\\poly1305_ameth.o",
                    "crypto\\poly1305\\poly1305_pmeth.o",
                    "crypto\\rand\\drbg_ctr.o",
                    "crypto\\rand\\drbg_lib.o",
                    "crypto\\rand\\rand_egd.o",
                    "crypto\\rand\\rand_err.o",
                    "crypto\\rand\\rand_lib.o",
                    "crypto\\rand\\rand_unix.o",
                    "crypto\\rand\\rand_vms.o",
                    "crypto\\rand\\rand_win.o",
                    "crypto\\rand\\randfile.o",
                    "crypto\\rsa\\rsa_ameth.o",
                    "crypto\\rsa\\rsa_asn1.o",
                    "crypto\\rsa\\rsa_chk.o",
                    "crypto\\rsa\\rsa_crpt.o",
                    "crypto\\rsa\\rsa_depr.o",
                    "crypto\\rsa\\rsa_err.o",
                    "crypto\\rsa\\rsa_gen.o",
                    "crypto\\rsa\\rsa_lib.o",
                    "crypto\\rsa\\rsa_meth.o",
                    "crypto\\rsa\\rsa_mp.o",
                    "crypto\\rsa\\rsa_none.o",
                    "crypto\\rsa\\rsa_oaep.o",
                    "crypto\\rsa\\rsa_ossl.o",
                    "crypto\\rsa\\rsa_pk1.o",
                    "crypto\\rsa\\rsa_pmeth.o",
                    "crypto\\rsa\\rsa_prn.o",
                    "crypto\\rsa\\rsa_pss.o",
                    "crypto\\rsa\\rsa_saos.o",
                    "crypto\\rsa\\rsa_sign.o",
                    "crypto\\rsa\\rsa_ssl.o",
                    "crypto\\rsa\\rsa_x931.o",
                    "crypto\\rsa\\rsa_x931g.o",
                    "crypto\\sha\\keccak1600-x86_64.o",
                    "crypto\\sha\\sha1-mb-x86_64.o",
                    "crypto\\sha\\sha1-x86_64.o",
                    "crypto\\sha\\sha1_one.o",
                    "crypto\\sha\\sha1dgst.o",
                    "crypto\\sha\\sha256-mb-x86_64.o",
                    "crypto\\sha\\sha256-x86_64.o",
                    "crypto\\sha\\sha256.o",
                    "crypto\\sha\\sha512-x86_64.o",
                    "crypto\\sha\\sha512.o",
                    "crypto\\stack\\stack.o",
                    "crypto\\store\\loader_file.o",
                    "crypto\\store\\store_err.o",
                    "crypto\\store\\store_init.o",
                    "crypto\\store\\store_lib.o",
                    "crypto\\store\\store_register.o",
                    "crypto\\store\\store_strings.o",
                    "crypto\\threads_none.o",
                    "crypto\\threads_pthread.o",
                    "crypto\\threads_win.o",
                    "crypto\\txt_db\\txt_db.o",
                    "crypto\\ui\\ui_err.o",
                    "crypto\\ui\\ui_lib.o",
                    "crypto\\ui\\ui_null.o",
                    "crypto\\ui\\ui_openssl.o",
                    "crypto\\ui\\ui_util.o",
                    "crypto\\uid.o",
                    "crypto\\x509\\by_dir.o",
                    "crypto\\x509\\by_file.o",
                    "crypto\\x509\\t_crl.o",
                    "crypto\\x509\\t_req.o",
                    "crypto\\x509\\t_x509.o",
                    "crypto\\x509\\x509_att.o",
                    "crypto\\x509\\x509_cmp.o",
                    "crypto\\x509\\x509_d2.o",
                    "crypto\\x509\\x509_def.o",
                    "crypto\\x509\\x509_err.o",
                    "crypto\\x509\\x509_ext.o",
                    "crypto\\x509\\x509_lu.o",
                    "crypto\\x509\\x509_meth.o",
                    "crypto\\x509\\x509_obj.o",
                    "crypto\\x509\\x509_r2x.o",
                    "crypto\\x509\\x509_req.o",
                    "crypto\\x509\\x509_set.o",
                    "crypto\\x509\\x509_trs.o",
                    "crypto\\x509\\x509_txt.o",
                    "crypto\\x509\\x509_v3.o",
                    "crypto\\x509\\x509_vfy.o",
                    "crypto\\x509\\x509_vpm.o",
                    "crypto\\x509\\x509cset.o",
                    "crypto\\x509\\x509name.o",
                    "crypto\\x509\\x509rset.o",
                    "crypto\\x509\\x509spki.o",
                    "crypto\\x509\\x509type.o",
                    "crypto\\x509\\x_all.o",
                    "crypto\\x509\\x_attrib.o",
                    "crypto\\x509\\x_crl.o",
                    "crypto\\x509\\x_exten.o",
                    "crypto\\x509\\x_name.o",
                    "crypto\\x509\\x_pubkey.o",
                    "crypto\\x509\\x_req.o",
                    "crypto\\x509\\x_x509.o",
                    "crypto\\x509\\x_x509a.o",
                    "crypto\\x509v3\\pcy_cache.o",
                    "crypto\\x509v3\\pcy_data.o",
                    "crypto\\x509v3\\pcy_lib.o",
                    "crypto\\x509v3\\pcy_map.o",
                    "crypto\\x509v3\\pcy_node.o",
                    "crypto\\x509v3\\pcy_tree.o",
                    "crypto\\x509v3\\v3_addr.o",
                    "crypto\\x509v3\\v3_admis.o",
                    "crypto\\x509v3\\v3_akey.o",
                    "crypto\\x509v3\\v3_akeya.o",
                    "crypto\\x509v3\\v3_alt.o",
                    "crypto\\x509v3\\v3_asid.o",
                    "crypto\\x509v3\\v3_bcons.o",
                    "crypto\\x509v3\\v3_bitst.o",
                    "crypto\\x509v3\\v3_conf.o",
                    "crypto\\x509v3\\v3_cpols.o",
                    "crypto\\x509v3\\v3_crld.o",
                    "crypto\\x509v3\\v3_enum.o",
                    "crypto\\x509v3\\v3_extku.o",
                    "crypto\\x509v3\\v3_genn.o",
                    "crypto\\x509v3\\v3_ia5.o",
                    "crypto\\x509v3\\v3_info.o",
                    "crypto\\x509v3\\v3_int.o",
                    "crypto\\x509v3\\v3_lib.o",
                    "crypto\\x509v3\\v3_ncons.o",
                    "crypto\\x509v3\\v3_pci.o",
                    "crypto\\x509v3\\v3_pcia.o",
                    "crypto\\x509v3\\v3_pcons.o",
                    "crypto\\x509v3\\v3_pku.o",
                    "crypto\\x509v3\\v3_pmaps.o",
                    "crypto\\x509v3\\v3_prn.o",
                    "crypto\\x509v3\\v3_purp.o",
                    "crypto\\x509v3\\v3_skey.o",
                    "crypto\\x509v3\\v3_sxnet.o",
                    "crypto\\x509v3\\v3_tlsf.o",
                    "crypto\\x509v3\\v3_utl.o",
                    "crypto\\x509v3\\v3err.o",
                    "crypto\\x86_64cpuid.o",
                    "engines\\e_capi.o",
                    "engines\\e_padlock-x86_64.o",
                    "engines\\e_padlock.o",
                ],
            "libssl" =>
                [
                    "ssl\\bio_ssl.o",
                    "ssl\\d1_lib.o",
                    "ssl\\d1_msg.o",
                    "ssl\\d1_srtp.o",
                    "ssl\\methods.o",
                    "ssl\\packet.o",
                    "ssl\\pqueue.o",
                    "ssl\\record\\dtls1_bitmap.o",
                    "ssl\\record\\rec_layer_d1.o",
                    "ssl\\record\\rec_layer_s3.o",
                    "ssl\\record\\ssl3_buffer.o",
                    "ssl\\record\\ssl3_record.o",
                    "ssl\\record\\ssl3_record_tls13.o",
                    "ssl\\s3_cbc.o",
                    "ssl\\s3_enc.o",
                    "ssl\\s3_lib.o",
                    "ssl\\s3_msg.o",
                    "ssl\\ssl_asn1.o",
                    "ssl\\ssl_cert.o",
                    "ssl\\ssl_ciph.o",
                    "ssl\\ssl_conf.o",
                    "ssl\\ssl_err.o",
                    "ssl\\ssl_init.o",
                    "ssl\\ssl_lib.o",
                    "ssl\\ssl_mcnf.o",
                    "ssl\\ssl_quic.o",
                    "ssl\\ssl_rsa.o",
                    "ssl\\ssl_sess.o",
                    "ssl\\ssl_stat.o",
                    "ssl\\ssl_txt.o",
                    "ssl\\ssl_utst.o",
                    "ssl\\statem\\extensions.o",
                    "ssl\\statem\\extensions_clnt.o",
                    "ssl\\statem\\extensions_cust.o",
                    "ssl\\statem\\extensions_srvr.o",
                    "ssl\\statem\\statem.o",
                    "ssl\\statem\\statem_clnt.o",
                    "ssl\\statem\\statem_dtls.o",
                    "ssl\\statem\\statem_lib.o",
                    "ssl\\statem\\statem_quic.o",
                    "ssl\\statem\\statem_srvr.o",
                    "ssl\\t1_enc.o",
                    "ssl\\t1_lib.o",
                    "ssl\\t1_trce.o",
                    "ssl\\tls13_enc.o",
                    "ssl\\tls_srp.o",
                ],
            "ssl\\bio_ssl.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\bio_ssl.c",
                ],
            "ssl\\d1_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\d1_lib.c",
                ],
            "ssl\\d1_msg.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\d1_msg.c",
                ],
            "ssl\\d1_srtp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\d1_srtp.c",
                ],
            "ssl\\methods.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\methods.c",
                ],
            "ssl\\packet.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\packet.c",
                ],
            "ssl\\pqueue.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\pqueue.c",
                ],
            "ssl\\record\\dtls1_bitmap.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\dtls1_bitmap.c",
                ],
            "ssl\\record\\rec_layer_d1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\rec_layer_d1.c",
                ],
            "ssl\\record\\rec_layer_s3.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\rec_layer_s3.c",
                ],
            "ssl\\record\\ssl3_buffer.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\ssl3_buffer.c",
                ],
            "ssl\\record\\ssl3_record.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\ssl3_record.c",
                ],
            "ssl\\record\\ssl3_record_tls13.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\record\\ssl3_record_tls13.c",
                ],
            "ssl\\s3_cbc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\s3_cbc.c",
                ],
            "ssl\\s3_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\s3_enc.c",
                ],
            "ssl\\s3_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\s3_lib.c",
                ],
            "ssl\\s3_msg.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\s3_msg.c",
                ],
            "ssl\\ssl_asn1.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_asn1.c",
                ],
            "ssl\\ssl_cert.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_cert.c",
                ],
            "ssl\\ssl_ciph.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_ciph.c",
                ],
            "ssl\\ssl_conf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_conf.c",
                ],
            "ssl\\ssl_err.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_err.c",
                ],
            "ssl\\ssl_init.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_init.c",
                ],
            "ssl\\ssl_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_lib.c",
                ],
            "ssl\\ssl_mcnf.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_mcnf.c",
                ],
            "ssl\\ssl_quic.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_quic.c",
                ],
            "ssl\\ssl_rsa.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_rsa.c",
                ],
            "ssl\\ssl_sess.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_sess.c",
                ],
            "ssl\\ssl_stat.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_stat.c",
                ],
            "ssl\\ssl_txt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_txt.c",
                ],
            "ssl\\ssl_utst.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\ssl_utst.c",
                ],
            "ssl\\statem\\extensions.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\extensions.c",
                ],
            "ssl\\statem\\extensions_clnt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\extensions_clnt.c",
                ],
            "ssl\\statem\\extensions_cust.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\extensions_cust.c",
                ],
            "ssl\\statem\\extensions_srvr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\extensions_srvr.c",
                ],
            "ssl\\statem\\statem.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem.c",
                ],
            "ssl\\statem\\statem_clnt.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem_clnt.c",
                ],
            "ssl\\statem\\statem_dtls.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem_dtls.c",
                ],
            "ssl\\statem\\statem_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem_lib.c",
                ],
            "ssl\\statem\\statem_quic.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem_quic.c",
                ],
            "ssl\\statem\\statem_srvr.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\statem\\statem_srvr.c",
                ],
            "ssl\\t1_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\t1_enc.c",
                ],
            "ssl\\t1_lib.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\t1_lib.c",
                ],
            "ssl\\t1_trce.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\t1_trce.c",
                ],
            "ssl\\tls13_enc.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\tls13_enc.c",
                ],
            "ssl\\tls_srp.o" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\ssl\\tls_srp.c",
                ],
            "tools\\c_rehash.pl" =>
                [
                    "..\\..\\..\\..\\..\\..\\msquic\\submodules\\openssl\\tools\\c_rehash.in",
                ],
        },
);

# The following data is only used when this files is use as a script
my @makevars = (
    'AR',
    'ARFLAGS',
    'AS',
    'ASFLAGS',
    'CC',
    'CFLAGS',
    'CPP',
    'CPPDEFINES',
    'CPPFLAGS',
    'CPPINCLUDES',
    'CROSS_COMPILE',
    'CXX',
    'CXXFLAGS',
    'HASHBANGPERL',
    'LD',
    'LDFLAGS',
    'LDLIBS',
    'MT',
    'MTFLAGS',
    'PERL',
    'RANLIB',
    'RC',
    'RCFLAGS',
    'RM',
);
my %disabled_info = (
    'afalgeng' => {
        macro => 'OPENSSL_NO_AFALGENG',
    },
    'aria' => {
        macro => 'OPENSSL_NO_ARIA',
        skipped => [ 'crypto\aria' ],
    },
    'asan' => {
        macro => 'OPENSSL_NO_ASAN',
    },
    'bf' => {
        macro => 'OPENSSL_NO_BF',
        skipped => [ 'crypto\bf' ],
    },
    'blake2' => {
        macro => 'OPENSSL_NO_BLAKE2',
        skipped => [ 'crypto\blake2' ],
    },
    'camellia' => {
        macro => 'OPENSSL_NO_CAMELLIA',
        skipped => [ 'crypto\camellia' ],
    },
    'cast' => {
        macro => 'OPENSSL_NO_CAST',
        skipped => [ 'crypto\cast' ],
    },
    'cms' => {
        macro => 'OPENSSL_NO_CMS',
        skipped => [ 'crypto\cms' ],
    },
    'comp' => {
        macro => 'OPENSSL_NO_COMP',
        skipped => [ 'crypto\comp' ],
    },
    'crypto-mdebug' => {
        macro => 'OPENSSL_NO_CRYPTO_MDEBUG',
    },
    'crypto-mdebug-backtrace' => {
        macro => 'OPENSSL_NO_CRYPTO_MDEBUG_BACKTRACE',
    },
    'ct' => {
        macro => 'OPENSSL_NO_CT',
        skipped => [ 'crypto\ct' ],
    },
    'devcryptoeng' => {
        macro => 'OPENSSL_NO_DEVCRYPTOENG',
    },
    'dgram' => {
        macro => 'OPENSSL_NO_DGRAM',
    },
    'dso' => {
        macro => 'OPENSSL_NO_DSO',
    },
    'dtls' => {
        macro => 'OPENSSL_NO_DTLS',
    },
    'dtls1' => {
        macro => 'OPENSSL_NO_DTLS1',
    },
    'dtls1-method' => {
        macro => 'OPENSSL_NO_DTLS1_METHOD',
    },
    'dtls1_2' => {
        macro => 'OPENSSL_NO_DTLS1_2',
    },
    'dtls1_2-method' => {
        macro => 'OPENSSL_NO_DTLS1_2_METHOD',
    },
    'ec2m' => {
        macro => 'OPENSSL_NO_EC2M',
    },
    'ec_nistp_64_gcc_128' => {
        macro => 'OPENSSL_NO_EC_NISTP_64_GCC_128',
    },
    'egd' => {
        macro => 'OPENSSL_NO_EGD',
    },
    'external-tests' => {
        macro => 'OPENSSL_NO_EXTERNAL_TESTS',
    },
    'fuzz-afl' => {
        macro => 'OPENSSL_NO_FUZZ_AFL',
    },
    'fuzz-libfuzzer' => {
        macro => 'OPENSSL_NO_FUZZ_LIBFUZZER',
    },
    'gost' => {
        macro => 'OPENSSL_NO_GOST',
    },
    'heartbeats' => {
        macro => 'OPENSSL_NO_HEARTBEATS',
    },
    'idea' => {
        macro => 'OPENSSL_NO_IDEA',
        skipped => [ 'crypto\idea' ],
    },
    'md2' => {
        macro => 'OPENSSL_NO_MD2',
        skipped => [ 'crypto\md2' ],
    },
    'md4' => {
        macro => 'OPENSSL_NO_MD4',
        skipped => [ 'crypto\md4' ],
    },
    'mdc2' => {
        macro => 'OPENSSL_NO_MDC2',
        skipped => [ 'crypto\mdc2' ],
    },
    'msan' => {
        macro => 'OPENSSL_NO_MSAN',
    },
    'ocb' => {
        macro => 'OPENSSL_NO_OCB',
    },
    'psk' => {
        macro => 'OPENSSL_NO_PSK',
    },
    'rc2' => {
        macro => 'OPENSSL_NO_RC2',
        skipped => [ 'crypto\rc2' ],
    },
    'rc4' => {
        macro => 'OPENSSL_NO_RC4',
        skipped => [ 'crypto\rc4' ],
    },
    'rc5' => {
        macro => 'OPENSSL_NO_RC5',
        skipped => [ 'crypto\rc5' ],
    },
    'ripemd' => {
        macro => 'OPENSSL_NO_RMD160',
        skipped => [ 'crypto\ripemd' ],
    },
    'scrypt' => {
        macro => 'OPENSSL_NO_SCRYPT',
    },
    'sctp' => {
        macro => 'OPENSSL_NO_SCTP',
    },
    'seed' => {
        macro => 'OPENSSL_NO_SEED',
        skipped => [ 'crypto\seed' ],
    },
    'siphash' => {
        macro => 'OPENSSL_NO_SIPHASH',
        skipped => [ 'crypto\siphash' ],
    },
    'sm2' => {
        macro => 'OPENSSL_NO_SM2',
        skipped => [ 'crypto\sm2' ],
    },
    'sm3' => {
        macro => 'OPENSSL_NO_SM3',
        skipped => [ 'crypto\sm3' ],
    },
    'sm4' => {
        macro => 'OPENSSL_NO_SM4',
        skipped => [ 'crypto\sm4' ],
    },
    'srp' => {
        macro => 'OPENSSL_NO_SRP',
        skipped => [ 'crypto\srp' ],
    },
    'srtp' => {
        macro => 'OPENSSL_NO_SRTP',
    },
    'ssl-trace' => {
        macro => 'OPENSSL_NO_SSL_TRACE',
    },
    'ssl3' => {
        macro => 'OPENSSL_NO_SSL3',
    },
    'ssl3-method' => {
        macro => 'OPENSSL_NO_SSL3_METHOD',
    },
    'tests' => {
        macro => 'OPENSSL_NO_TESTS',
    },
    'tls1' => {
        macro => 'OPENSSL_NO_TLS1',
    },
    'tls1-method' => {
        macro => 'OPENSSL_NO_TLS1_METHOD',
    },
    'tls1_1' => {
        macro => 'OPENSSL_NO_TLS1_1',
    },
    'tls1_1-method' => {
        macro => 'OPENSSL_NO_TLS1_1_METHOD',
    },
    'tls1_2' => {
        macro => 'OPENSSL_NO_TLS1_2',
    },
    'tls1_2-method' => {
        macro => 'OPENSSL_NO_TLS1_2_METHOD',
    },
    'ts' => {
        macro => 'OPENSSL_NO_TS',
        skipped => [ 'crypto\ts' ],
    },
    'ubsan' => {
        macro => 'OPENSSL_NO_UBSAN',
    },
    'unit-test' => {
        macro => 'OPENSSL_NO_UNIT_TEST',
    },
    'weak-ssl-ciphers' => {
        macro => 'OPENSSL_NO_WEAK_SSL_CIPHERS',
    },
    'whrlpool' => {
        macro => 'OPENSSL_NO_WHIRLPOOL',
        skipped => [ 'crypto\whrlpool' ],
    },
);
my @user_crossable = qw( AR AS CC CXX CPP LD MT RANLIB RC );
# If run directly, we can give some answers, and even reconfigure
unless (caller) {
    use Getopt::Long;
    use File::Spec::Functions;
    use File::Basename;
    use Pod::Usage;

    my $here = dirname($0);

    my $dump = undef;
    my $cmdline = undef;
    my $options = undef;
    my $target = undef;
    my $envvars = undef;
    my $makevars = undef;
    my $buildparams = undef;
    my $reconf = undef;
    my $verbose = undef;
    my $help = undef;
    my $man = undef;
    GetOptions('dump|d'                 => \$dump,
               'command-line|c'         => \$cmdline,
               'options|o'              => \$options,
               'target|t'               => \$target,
               'environment|e'          => \$envvars,
               'make-variables|m'       => \$makevars,
               'build-parameters|b'     => \$buildparams,
               'reconfigure|reconf|r'   => \$reconf,
               'verbose|v'              => \$verbose,
               'help'                   => \$help,
               'man'                    => \$man)
        or die "Errors in command line arguments\n";

    unless ($dump || $cmdline || $options || $target || $envvars || $makevars
            || $buildparams || $reconf || $verbose || $help || $man) {
        print STDERR <<"_____";
You must give at least one option.
For more information, do '$0 --help'
_____
        exit(2);
    }

    if ($help) {
        pod2usage(-exitval => 0,
                  -verbose => 1);
    }
    if ($man) {
        pod2usage(-exitval => 0,
                  -verbose => 2);
    }
    if ($dump || $cmdline) {
        print "\nCommand line (with current working directory = $here):\n\n";
        print '    ',join(' ',
                          $config{PERL},
                          catfile($config{sourcedir}, 'Configure'),
                          @{$config{perlargv}}), "\n";
        print "\nPerl information:\n\n";
        print '    ',$config{perl_cmd},"\n";
        print '    ',$config{perl_version},' for ',$config{perl_archname},"\n";
    }
    if ($dump || $options) {
        my $longest = 0;
        my $longest2 = 0;
        foreach my $what (@disablables) {
            $longest = length($what) if $longest < length($what);
            $longest2 = length($disabled{$what})
                if $disabled{$what} && $longest2 < length($disabled{$what});
        }
        print "\nEnabled features:\n\n";
        foreach my $what (@disablables) {
            print "    $what\n"
                unless grep { $_ =~ /^${what}$/ } keys %disabled;
        }
        print "\nDisabled features:\n\n";
        foreach my $what (@disablables) {
            my @what2 = grep { $_ =~ /^${what}$/ } keys %disabled;
            my $what3 = $what2[0];
            if ($what3) {
                print "    $what3", ' ' x ($longest - length($what3) + 1),
                    "[$disabled{$what3}]", ' ' x ($longest2 - length($disabled{$what3}) + 1);
                print $disabled_info{$what3}->{macro}
                    if $disabled_info{$what3}->{macro};
                print ' (skip ',
                    join(', ', @{$disabled_info{$what3}->{skipped}}),
                    ')'
                    if $disabled_info{$what3}->{skipped};
                print "\n";
            }
        }
    }
    if ($dump || $target) {
        print "\nConfig target attributes:\n\n";
        foreach (sort keys %target) {
            next if $_ =~ m|^_| || $_ eq 'template';
            my $quotify = sub {
                map { (my $x = $_) =~ s|([\\\$\@"])|\\$1|g; "\"$x\""} @_;
            };
            print '    ', $_, ' => ';
            if (ref($target{$_}) eq "ARRAY") {
                print '[ ', join(', ', $quotify->(@{$target{$_}})), " ],\n";
            } else {
                print $quotify->($target{$_}), ",\n"
            }
        }
    }
    if ($dump || $envvars) {
        print "\nRecorded environment:\n\n";
        foreach (sort keys %{$config{perlenv}}) {
            print '    ',$_,' = ',($config{perlenv}->{$_} || ''),"\n";
        }
    }
    if ($dump || $makevars) {
        print "\nMakevars:\n\n";
        foreach my $var (@makevars) {
            my $prefix = '';
            $prefix = $config{CROSS_COMPILE}
                if grep { $var eq $_ } @user_crossable;
            $prefix //= '';
            print '    ',$var,' ' x (16 - length $var),'= ',
                (ref $config{$var} eq 'ARRAY'
                 ? join(' ', @{$config{$var}})
                 : $prefix.$config{$var}),
                "\n"
                if defined $config{$var};
        }

        my @buildfile = ($config{builddir}, $config{build_file});
        unshift @buildfile, $here
            unless file_name_is_absolute($config{builddir});
        my $buildfile = canonpath(catdir(@buildfile));
        print <<"_____";

NOTE: These variables only represent the configuration view.  The build file
template may have processed these variables further, please have a look at the
build file for more exact data:
    $buildfile
_____
    }
    if ($dump || $buildparams) {
        my @buildfile = ($config{builddir}, $config{build_file});
        unshift @buildfile, $here
            unless file_name_is_absolute($config{builddir});
        print "\nbuild file:\n\n";
        print "    ", canonpath(catfile(@buildfile)),"\n";

        print "\nbuild file templates:\n\n";
        foreach (@{$config{build_file_templates}}) {
            my @tmpl = ($_);
            unshift @tmpl, $here
                unless file_name_is_absolute($config{sourcedir});
            print '    ',canonpath(catfile(@tmpl)),"\n";
        }
    }
    if ($reconf) {
        if ($verbose) {
            print 'Reconfiguring with: ', join(' ',@{$config{perlargv}}), "\n";
            foreach (sort keys %{$config{perlenv}}) {
                print '    ',$_,' = ',($config{perlenv}->{$_} || ""),"\n";
            }
        }

        chdir $here;
        exec $^X,catfile($config{sourcedir}, 'Configure'),'reconf';
    }
}

1;

__END__

=head1 NAME

configdata.pm - configuration data for OpenSSL builds

=head1 SYNOPSIS

Interactive:

  perl configdata.pm [options]

As data bank module:

  use configdata;

=head1 DESCRIPTION

This module can be used in two modes, interactively and as a module containing
all the data recorded by OpenSSL's Configure script.

When used interactively, simply run it as any perl script, with at least one
option, and you will get the information you ask for.  See L</OPTIONS> below.

When loaded as a module, you get a few databanks with useful information to
perform build related tasks.  The databanks are:

    %config             Configured things.
    %target             The OpenSSL config target with all inheritances
                        resolved.
    %disabled           The features that are disabled.
    @disablables        The list of features that can be disabled.
    %withargs           All data given through --with-THING options.
    %unified_info       All information that was computed from the build.info
                        files.

=head1 OPTIONS

=over 4

=item B<--help>

Print a brief help message and exit.

=item B<--man>

Print the manual page and exit.

=item B<--dump> | B<-d>

Print all relevant configuration data.  This is equivalent to B<--command-line>
B<--options> B<--target> B<--environment> B<--make-variables>
B<--build-parameters>.

=item B<--command-line> | B<-c>

Print the current configuration command line.

=item B<--options> | B<-o>

Print the features, both enabled and disabled, and display defined macro and
skipped directories where applicable.

=item B<--target> | B<-t>

Print the config attributes for this config target.

=item B<--environment> | B<-e>

Print the environment variables and their values at the time of configuration.

=item B<--make-variables> | B<-m>

Print the main make variables generated in the current configuration

=item B<--build-parameters> | B<-b>

Print the build parameters, i.e. build file and build file templates.

=item B<--reconfigure> | B<--reconf> | B<-r>

Redo the configuration.

=item B<--verbose> | B<-v>

Verbose output.

=back

=cut

