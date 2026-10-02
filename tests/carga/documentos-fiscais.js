import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import exec from 'k6/execution';

const urlBase = (__ENV.BASE_URL || 'http://localhost:5119').replace(/\/$/, '');
const rotaDocumentos = `${urlBase}/api/v1/documentos-fiscais`;

const errosUpload = new Rate('erros_upload');
const errosReenvio = new Rate('erros_reenvio');
const errosListagem = new Rate('erros_listagem');
const errosConsulta = new Rate('erros_consulta');

const latenciaUpload = new Trend('latencia_upload', true);
const latenciaReenvio = new Trend('latencia_reenvio', true);
const latenciaListagem = new Trend('latencia_listagem', true);
const latenciaConsulta = new Trend('latencia_consulta', true);

const duracaoUpload = __ENV.DURACAO_UPLOAD || '40s';
const duracaoReenvio = __ENV.DURACAO_REENVIO || '30s';
const duracaoLeituras = __ENV.DURACAO_LEITURAS || '30s';
const vusUpload = Number(__ENV.VUS_UPLOAD || 5);
const vusReenvio = Number(__ENV.VUS_REENVIO || 2);
const taxaListagem = Number(__ENV.TAXA_LISTAGEM || 5);
const taxaConsulta = Number(__ENV.TAXA_CONSULTA || 5);

export const options = {
  discardResponseBodies: false,
  scenarios: {
    upload_concorrente: {
      executor: 'constant-vus',
      exec: 'executarUploadConcorrente',
      vus: vusUpload,
      duration: duracaoUpload,
      gracefulStop: '5s',
    },
    reenvio_mesmo_documento: {
      executor: 'constant-vus',
      exec: 'executarReenvio',
      vus: vusReenvio,
      duration: duracaoReenvio,
      gracefulStop: '5s',
    },
    listagem_paginada: {
      executor: 'constant-arrival-rate',
      exec: 'executarListagemPaginada',
      rate: taxaListagem,
      timeUnit: '1s',
      duration: duracaoLeituras,
      preAllocatedVUs: Math.max(2, taxaListagem),
      maxVUs: Math.max(10, taxaListagem * 2),
    },
    consulta_por_identificador: {
      executor: 'constant-arrival-rate',
      exec: 'executarConsultaPorIdentificador',
      rate: taxaConsulta,
      timeUnit: '1s',
      duration: duracaoLeituras,
      preAllocatedVUs: Math.max(2, taxaConsulta),
      maxVUs: Math.max(10, taxaConsulta * 2),
    },
  },
  thresholds: {
    erros_upload: ['rate<0.01'],
    erros_reenvio: ['rate<0.01'],
    erros_listagem: ['rate<0.01'],
    erros_consulta: ['rate<0.01'],
    latencia_upload: [`p(95)<${Number(__ENV.P95_UPLOAD_MS || 2000)}`],
    latencia_reenvio: [`p(95)<${Number(__ENV.P95_REENVIO_MS || 1000)}`],
    latencia_listagem: [`p(95)<${Number(__ENV.P95_LISTAGEM_MS || 500)}`],
    latencia_consulta: [`p(95)<${Number(__ENV.P95_CONSULTA_MS || 500)}`],
  },
};

export function setup() {
  const prefixoExecucao = String(Date.now()).slice(-11);
  const chave = criarChaveFiscal(prefixoExecucao, 0);
  const xml = criarNFe(chave);
  const resposta = enviarXml(xml, `semente-${chave}.xml`, { operacao: 'preparacao' });

  const criado = check(resposta, {
    'preparação criou ou reutilizou o documento': (resultado) =>
      resultado.status === 201 || resultado.status === 200,
  });

  if (!criado) {
    throw new Error(`Não foi possível preparar os dados. HTTP ${resposta.status}.`);
  }

  const documento = resposta.json();
  if (!documento || !documento.id) {
    throw new Error('A preparação não retornou o identificador do documento.');
  }

  return {
    prefixoExecucao,
    documentoId: documento.id,
    xmlReenvio: xml,
  };
}

export function executarUploadConcorrente(dados) {
  const numeroUnico = exec.scenario.iterationInTest + 1;
  const chave = criarChaveFiscal(dados.prefixoExecucao, numeroUnico);
  const resposta = enviarXml(
    criarNFe(chave),
    `carga-${chave}.xml`,
    { operacao: 'upload_concorrente' },
  );

  const sucesso = check(resposta, {
    'upload retornou 201': (resultado) => resultado.status === 201,
    'upload retornou identificador': (resultado) => Boolean(resultado.json('id')),
  });

  errosUpload.add(!sucesso);
  latenciaUpload.add(resposta.timings.duration);
  sleep(0.2);
}

export function executarReenvio(dados) {
  const resposta = enviarXml(
    dados.xmlReenvio,
    'documento-reenviado.xml',
    { operacao: 'reenvio' },
  );

  const sucesso = check(resposta, {
    'reenvio retornou 200': (resultado) => resultado.status === 200,
    'reenvio foi reconhecido como idempotente': (resultado) =>
      resultado.headers['Idempotent-Replay'] === 'true',
    'reenvio manteve o identificador': (resultado) =>
      resultado.json('id') === dados.documentoId,
  });

  errosReenvio.add(!sucesso);
  latenciaReenvio.add(resposta.timings.duration);
  sleep(0.2);
}

export function executarListagemPaginada() {
  const pagina = (exec.scenario.iterationInTest % 5) + 1;
  const resposta = http.get(
    `${rotaDocumentos}?pagina=${pagina}&tamanhoPagina=20`,
    { tags: { operacao: 'listagem_paginada' } },
  );

  const sucesso = check(resposta, {
    'listagem retornou 200': (resultado) => resultado.status === 200,
    'listagem retornou página solicitada': (resultado) =>
      resultado.json('pagina') === pagina,
    'listagem retornou itens': (resultado) => Array.isArray(resultado.json('itens')),
  });

  errosListagem.add(!sucesso);
  latenciaListagem.add(resposta.timings.duration);
}

export function executarConsultaPorIdentificador(dados) {
  const resposta = http.get(
    `${rotaDocumentos}/${dados.documentoId}`,
    { tags: { operacao: 'consulta_por_identificador' } },
  );

  const sucesso = check(resposta, {
    'consulta retornou 200': (resultado) => resultado.status === 200,
    'consulta retornou o documento esperado': (resultado) =>
      resultado.json('id') === dados.documentoId,
  });

  errosConsulta.add(!sucesso);
  latenciaConsulta.add(resposta.timings.duration);
}

function enviarXml(xml, nomeArquivo, tags) {
  return http.post(
    rotaDocumentos,
    {
      arquivo: http.file(xml, nomeArquivo, 'application/xml'),
    },
    { tags },
  );
}

function criarChaveFiscal(prefixoExecucao, numeroUnico) {
  const parteVariavel = `${prefixoExecucao}${String(numeroUnico).padStart(12, '0')}`;
  return `35${parteVariavel.padStart(42, '0').slice(-42)}`;
}

function criarNFe(chave) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
  <NFe>
    <infNFe Id="NFe${chave}" versao="4.00">
      <ide><dhEmi>2026-10-02T12:00:00-03:00</dhEmi></ide>
      <emit>
        <CNPJ>12345678000195</CNPJ>
        <enderEmit><UF>SP</UF></enderEmit>
      </emit>
      <dest><CNPJ>98765432000198</CNPJ></dest>
    </infNFe>
  </NFe>
</nfeProc>`;
}
