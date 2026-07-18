#pragma once

#include <string_view>

const char* GetName();
std::string_view GetNameView();
int MeasureNameView(std::string_view value);
