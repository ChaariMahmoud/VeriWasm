#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <binaryen-c.h>
#define EXPORT __attribute__((visibility("default")))


// ✅ Charge un module WASM binaire
BinaryenModuleRef LoadWasmTextFile(const char* filename) {
    FILE* file = fopen(filename, "rb");
    if (!file) {
        fprintf(stderr, "Error: Cannot open file %s\n", filename);
        return NULL;
    }

    fseek(file, 0, SEEK_END);
    size_t size = ftell(file);
    rewind(file);

    char* buffer = (char*)malloc(size);
    if (!buffer) {
        fprintf(stderr, "Error: Memory allocation failed.\n");
        fclose(file);
        return NULL;
    }

    fread(buffer, 1, size, file);
    fclose(file);

    BinaryenModuleRef module = BinaryenModuleRead(buffer, size);
    free(buffer);
    return module;
}

// ✅ Retourne le nombre de fonctions du module
int GetFunctionCount(BinaryenModuleRef module) {
    return BinaryenGetNumFunctions(module);
}

// ✅ Retourne le nom de la première fonction
const char* GetFirstFunctionName(BinaryenModuleRef module) {
    if (BinaryenGetNumFunctions(module) == 0) return "";
    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, 0);
    return BinaryenFunctionGetName(func);
}

// ✅ Affiche l'AST du module (sous forme textuelle, très utile pour debug)
void PrintModuleAST(BinaryenModuleRef module) {
    char* watText = BinaryenModuleAllocateAndWriteText(module);
    if (watText != NULL) {
        printf("\n===== AST WAT (depuis Binaryen) =====\n");
        printf("%s\n", watText);
        printf("=====================================\n\n");
        free(watText);
    } else {
        fprintf(stderr, "❌ Impossible d'obtenir l'AST textuel du module.\n");
    }
}

// 🔒 Fonction de validation du module
bool ValidateModule(BinaryenModuleRef module) {
    return BinaryenModuleValidate(module);
}

// ✅ Récupère le corps d'une fonction (expression racine)
BinaryenExpressionRef GetFunctionBody(BinaryenModuleRef module, int index) {
    if (index >= BinaryenGetNumFunctions(module)) return NULL;
    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, index);
    return BinaryenFunctionGetBody(func);
}

// ✅ Récupère l'identifiant (type) d'une expression
int GetExpressionId(BinaryenExpressionRef expr) {
    return BinaryenExpressionGetId(expr);
}

// ✅ Retourne le corps WAT d'une fonction sous forme de chaîne


EXPORT const char* GetFunctionBodyText(BinaryenModuleRef module, int index) {
    if (index >= BinaryenGetNumFunctions(module)) return "";

    BinaryenFunctionRef func = BinaryenGetFunctionByIndex(module, index);
    if (!func) return "";

    BinaryenExpressionRef body = BinaryenFunctionGetBody(func);
    if (!body) return "";

    // On crée un nouveau module temporaire pour encapsuler le corps
    BinaryenModuleRef tempMod = BinaryenModuleCreate();
    BinaryenExpressionRef copied = BinaryenExpressionCopy(body, tempMod);


    BinaryenAddFunction(tempMod, "temp", BinaryenTypeNone(), BinaryenTypeNone(), NULL, 0, copied);

    char* wat = BinaryenModuleAllocateAndWriteText(tempMod);
    char* result = strdup(wat); // Important : faire une copie persistante
    free(wat);
    BinaryenModuleDispose(tempMod);
    return result;
}

