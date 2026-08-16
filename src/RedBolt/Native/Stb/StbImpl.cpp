// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#define STB_IMAGE_IMPLEMENTATION 1
#include "stb_image.h"

#define STB_IMAGE_WRITE_IMPLEMENTATION 1
#include "stb_image_write.h"

#undef STB_VORBIS_HEADER_ONLY
#include "stb_vorbis.c"

#define STB_TRUETYPE_IMPLEMENTATION 1
#include "stb_truetype.h"

// So far only imgui is using textedit. Also use of this header requires extensive configuration. Due to these reasons
// header implementation is not enabled here until other parts of project require it.
//#define STB_TEXTEDIT_IMPLEMENTATION 1
//#include "stb_textedit.h"
