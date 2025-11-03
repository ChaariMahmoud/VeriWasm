// binaryen_wrapper.c
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#include <binaryen-c.h>

#define EXPORT __attribute__((visibility("default")))

/* =========================
 * Module loading / printing
 * ========================= */

/// Load a compiled WASM binary (.wasm) into a Binaryen module
EXPORT BinaryenModuleRef LoadWasmTextFile(const char* filename) {
    FILE* file = fopen(filename, "rb");
    if (!file) {
        fprintf(stderr, "Error: Cannot open file %s\n", filename);
        return NULL;
    }

    fseek(file, 0, SEEK_END);
    long fsize_long = ftell(file);
    if (fsize_long < 0) {
        fclose(file);
        fprintf(stderr, "Error: ftell failed for %s\n", filename);
        return NULL;
    }
    size_t fsize = (size_t)fsize_long;
    rewind(file);

    char* buffer = (char*)malloc(fsize);
    if (!buffer) {
        fprintf(stderr, "Error: Memory allocation failed.\n");
        fclose(file);
        return NULL;
    }

    size_t readn = fread(buffer, 1, fsize, file);
    fclose(file);
    if (readn != fsize) {
        free(buffer);
        fprintf(stderr, "Error: fread failed (read %zu of %zu)\n", readn, fsize);
        return NULL;
    }

    BinaryenModuleRef module = BinaryenModuleRead(buffer, fsize);
    free(buffer);
    return module;
}

/// Validate a Binaryen module
EXPORT bool ValidateModule(BinaryenModuleRef module) {
    if (!module) return false;
    return BinaryenModuleValidate(module);
}

/// Print the module as WAT (for debugging)
EXPORT void PrintModuleAST(BinaryenModuleRef module) {
    if (!module) {
        fprintf(stderr, "❌ PrintModuleAST: null module\n");
        return;
    }
    char* watText = BinaryenModuleAllocateAndWriteText(module);
    if (watText) {
        printf("\n===== AST WAT (from Binaryen) =====\n");
        printf("%s\n", watText);
        printf("=====================================\n\n");
        free(watText);
    } else {
        fprintf(stderr, "❌ Unable to obtain the textual AST of the module.\n");
    }
}

/* =========================
 * Function enumeration
 * ========================= */

/// Number of functions in the module
EXPORT int GetFunctionCount(BinaryenModuleRef module) {
    if (!module) return 0;
    return BinaryenGetNumFunctions(module);
}

/// Get function name by index (as Binaryen sees it, e.g. "$0", "$foo")
/// Returned pointer is owned by Binaryen; DO NOT free it.
EXPORT const char* GetFunctionNameByIndex(BinaryenModuleRef module, int index) {
    if (!module) return "";
    int n = BinaryenGetNumFunctions(module);
    if (index < 0 || index >= n) return "";
    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, index);
    if (!func) return "";
    const char* name = BinaryenFunctionGetName(func);
    return name ? name : "";
}

/* =========================
 * Function body as WAT
 * ========================= */

/// Get the root expression of a function (for completeness)
EXPORT BinaryenExpressionRef GetFunctionBody(BinaryenModuleRef module, int index) {
    if (!module) return NULL;
    int n = BinaryenGetNumFunctions(module);
    if (index < 0 || index >= n) return NULL;
    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, index);
    if (!func) return NULL;
    return BinaryenFunctionGetBody(func);
}

EXPORT int GetFunctionResultCount(BinaryenModuleRef module, int index) {
    if (!module) return 0;
    int n = BinaryenGetNumFunctions(module);
    if (index < 0 || index >= n) return 0;
    BinaryenFunctionRef f = BinaryenGetFunctionByIndex(module, index);
    if (!f) return 0;
    BinaryenType results = BinaryenFunctionGetResults(f);
    return BinaryenTypeArity(results);
}

EXPORT int GetFunctionParamCount(BinaryenModuleRef module, int index) {
    if (!module) return 0;
    int n = BinaryenGetNumFunctions(module);
    if (index < 0 || index >= n) return 0;
    BinaryenFunctionRef f = BinaryenGetFunctionByIndex(module, index);
    if (!f) return 0;
    BinaryenType params = BinaryenFunctionGetParams(f);
    return BinaryenTypeArity(params);
}


/// Return a WAT string containing a tiny (module (func $temp ...)) that wraps
/// the copied body of the function at `index`.
/// The returned C string is heap-allocated (via strdup); call FreeCString() from C#
EXPORT const char* GetFunctionBodyText(BinaryenModuleRef module, int index) {
    if (!module) return strdup("");

    int n = BinaryenGetNumFunctions(module);
    if (index < 0 || index >= n) return strdup("");

    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, index);
    if (!func) return strdup("");

    BinaryenExpressionRef body = BinaryenFunctionGetBody(func);
    if (!body) return strdup("");

    // *** NOUVEAU : récupérer signature exacte ***
    BinaryenType params  = BinaryenFunctionGetParams(func);
    BinaryenType results = BinaryenFunctionGetResults(func);

    BinaryenModuleRef tempMod = BinaryenModuleCreate();
    if (!tempMod) return strdup("");

    BinaryenExpressionRef copied = BinaryenExpressionCopy(body, tempMod);

    // *** NOUVEAU : récréer la fonction avec la VRAIE signature ***
    BinaryenAddFunction(
        tempMod,
        "temp",
        params,
        results,
        NULL, 0,
        copied
    );

    char* wat = BinaryenModuleAllocateAndWriteText(tempMod);
    const char* result = wat ? strdup(wat) : strdup("");
    if (wat) free(wat);

    BinaryenModuleDispose(tempMod);
    return result;
}

/// Free a C-string returned by GetFunctionBodyText (since we used strdup)
EXPORT void FreeCString(const char* s) {
    if (s) free((void*)s);
}
