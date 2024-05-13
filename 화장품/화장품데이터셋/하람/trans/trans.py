import pandas as pd
from googletrans import Translator, LANGUAGES
from tqdm import tqdm

tqdm.pandas()
# 엑셀 파일 불러오기
df = pd.read_excel("하람성분 제품.xlsx")

# Translator 객체 초기화
translator = Translator()

# 번역 함수 정의
def translate_text(text, dest_language="en"):
    if pd.isna(text):
        return text
    try:
        return translator.translate(text, dest=dest_language).text
    except Exception as e:
        return text

# 'Brand'와 'Product Name' 컬럼 번역
df['BN_EN_NAME'] = df['브랜드'].progress_apply(lambda x: translate_text(x, "en"))
df['PN_EN_NAME'] = df['상품명'].progress_apply(lambda x: translate_text(x, "en"))

# 번역된 데이터 저장
df.to_excel("translated_file.xlsx")
